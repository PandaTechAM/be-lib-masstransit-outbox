using System.Data;
using System.Text.Json;
using EFCore.PostgresExtensions.Enums;
using EFCore.PostgresExtensions.Extensions;
using MassTransit.PostgresOutbox.Entities;
using MassTransit.PostgresOutbox.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MassTransit.PostgresOutbox.Abstractions;

/// <summary>
///     Base class for idempotent message consumption using the inbox pattern with PostgreSQL.
///     Inherit from this class and implement
///     <see cref="ConsumeAsync(TMessage, IDbContextTransaction, CancellationToken)" />
///     to process messages exactly once. Concurrency is controlled via <c>FOR UPDATE SKIP LOCKED</c>.
/// </summary>
/// <typeparam name="TMessage">The message type to consume.</typeparam>
/// <typeparam name="TDbContext">The <see cref="DbContext" /> type that implements <see cref="IInboxDbContext" />.</typeparam>
public abstract class InboxConsumer<TMessage, TDbContext> : IConsumer<TMessage>
    where TMessage : class
    where TDbContext : DbContext, IInboxDbContext
{
    private readonly string _consumerId;
    private readonly IServiceProvider _sp;

    /// <summary>
    ///     Initializes the consumer with the service provider used to resolve the DbContext,
    ///     logger, and settings for each message.
    /// </summary>
    /// <param name="sp">The application service provider.</param>
    protected InboxConsumer(IServiceProvider sp)
    {
        _consumerId = GetType()
            .ToString();
        _sp = sp;
    }

    /// <summary>
    ///     MassTransit entry point. Records the message for idempotency, locks it, and dispatches to
    ///     <see cref="ConsumeAsync(TMessage, IDbContextTransaction, CancellationToken)" />. Do not override —
    ///     put domain logic in <c>ConsumeAsync</c>.
    /// </summary>
    /// <param name="context">The MassTransit consume context.</param>
    public async Task Consume(ConsumeContext<TMessage> context)
    {
        var ct = context.CancellationToken;
        var messageId = context.Headers.Get<Guid>(Constants.OutboxMessageIdHeaderName) ?? context.MessageId;
        var dbContext = _sp.GetRequiredService<TDbContext>();
        var logger = _sp.GetRequiredService<ILogger<InboxConsumer<TMessage, TDbContext>>>();
        var settings = _sp.GetRequiredService<Settings>();

        var exists = await dbContext.InboxMessages
            .AnyAsync(x => x.MessageId == messageId && x.ConsumerId == _consumerId, ct);

        if (!exists)
        {
            try
            {
                dbContext.InboxMessages.Add(new InboxMessage
                {
                    MessageId = messageId!.Value,
                    CreatedAt = DateTime.UtcNow,
                    State = MessageState.New,
                    ConsumerId = _consumerId,
                    Payload = settings.InboxRetryEnabled ? JsonSerializer.Serialize(context.Message) : null,
                    Type = settings.InboxRetryEnabled ? GetVersionAgnosticTypeName() : null,
                    DestinationAddress = settings.InboxRetryEnabled
                        ? context.ReceiveContext.InputAddress?.ToString()
                        : null
                });
                await dbContext.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                dbContext.ChangeTracker.Clear();
            }
        }


        await using var transactionScope = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        var inboxMessage = await dbContext.InboxMessages
            .Where(x => x.MessageId == messageId)
            .Where(x => x.ConsumerId == _consumerId)
            .Where(x => x.State == MessageState.New)
            .ForUpdate(LockBehavior.SkipLocked)
            .FirstOrDefaultAsync(ct);
        if (inboxMessage is null)
        {
            await transactionScope.RollbackAsync(CancellationToken.None);
            return;
        }

        try
        {
            await ConsumeAsync(context.Message, transactionScope, ct);

            inboxMessage.State = MessageState.Done;
            inboxMessage.UpdatedAt = DateTime.UtcNow;

            await dbContext.SaveChangesAsync(ct);
            await transactionScope.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            InboxLog.ConsumeError(logger, messageId, _consumerId, ex);

            await transactionScope.RollbackAsync(CancellationToken.None);

            if (!settings.InboxRetryEnabled)
            {
                throw;
            }

            var newRetryCount = inboxMessage.RetryCount + 1;

            var isFailed = newRetryCount > settings.InboxRetryIntervals.Length;

            var nextRetryAt = isFailed
                ? (DateTime?)null
                : RetryDelayCalculator.ComputeNextRetryAt(inboxMessage.RetryCount, settings);

            var newState = isFailed ? MessageState.Failed : MessageState.New;

            var errorMessage = ex.ToString();

            if (errorMessage.Length > 4000)
            {
                errorMessage = errorMessage[..4000];
            }

            await dbContext.InboxMessages
                .Where(x => x.MessageId == messageId && x.ConsumerId == _consumerId)
                .ExecuteUpdateAsync(x => x.SetProperty(p => p.UpdatedAt, DateTime.UtcNow)
                        .SetProperty(p => p.RetryCount, newRetryCount)
                        .SetProperty(p => p.State, newState)
                        .SetProperty(p => p.NextRetryAt, nextRetryAt)
                        .SetProperty(p => p.LastError, errorMessage),
                    CancellationToken.None);

            if (isFailed)
            {
                InboxLog.RetryExhausted(logger, messageId, _consumerId, newRetryCount);
            }
            else
            {
                InboxLog.RetryScheduled(logger, messageId, _consumerId, newRetryCount, nextRetryAt!.Value);
            }
        }
    }

    /// <summary>
    ///     Processes the incoming message within a managed transaction. The base class
    ///     handles idempotency, locking, commit, and rollback — implementations should
    ///     only contain domain logic.
    /// </summary>
    /// <param name="message">The deserialized message payload.</param>
    /// <param name="transactionScope">
    ///     The active database transaction. Enlist additional operations in this
    ///     transaction if they must be atomic with the inbox state change.
    /// </param>
    /// <param name="ct">Cancellation token propagated from the MassTransit consume pipeline.</param>
    protected abstract Task ConsumeAsync(TMessage message, IDbContextTransaction transactionScope,
        CancellationToken ct);

    private static string GetVersionAgnosticTypeName()
    {
        var type = typeof(TMessage);
        return $"{type.FullName}, {type.Assembly.GetName().Name}";
    }
}

internal static partial class InboxLog
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to consume inbox message {MessageId} by {ConsumerId}")]
    public static partial void ConsumeError(ILogger logger, Guid? messageId, string consumerId, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Inbox message {MessageId} for {ConsumerId} scheduled for retry {RetryCount} at {NextRetryAt}")]
    public static partial void RetryScheduled(ILogger logger,
        Guid? messageId,
        string consumerId,
        int retryCount,
        DateTime nextRetryAt);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Inbox message {MessageId} for {ConsumerId} exhausted all {RetryCount} retry attempts")]
    public static partial void RetryExhausted(ILogger logger, Guid? messageId, string consumerId, int retryCount);
}
