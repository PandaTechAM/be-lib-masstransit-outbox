using MassTransit.PostgresOutbox.Entities;
using Microsoft.EntityFrameworkCore;

namespace MassTransit.PostgresOutbox.Extensions;

/// <summary>
///     EF Core model configuration helpers for the outbox and inbox entities.
/// </summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    ///     Configures the OutboxMessage entity (primary key, value generation).
    /// </summary>
    public static ModelBuilder ConfigureOutboxMessageEntity(this ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<OutboxMessage>();

        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id)
            .ValueGeneratedNever();

        return modelBuilder;
    }

    /// <summary>
    ///     Configures the InboxMessage entity (composite primary key).
    /// </summary>
    public static ModelBuilder ConfigureInboxMessageEntity(this ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<InboxMessage>();

        entity.HasKey(x => new
        {
            x.MessageId,
            x.ConsumerId
        });

        entity.HasIndex(x => new { x.State, x.RetryCount, x.NextRetryAt });

        return modelBuilder;
    }

    /// <summary>
    ///     Configures both inbox and outbox entity mappings. Call this in
    ///     <see cref="DbContext.OnModelCreating" /> for any context implementing
    ///     <see cref="Abstractions.IOutboxDbContext" /> and/or <see cref="Abstractions.IInboxDbContext" />.
    /// </summary>
    public static ModelBuilder ConfigureInboxOutboxEntities(this ModelBuilder modelBuilder)
    {
        modelBuilder.ConfigureOutboxMessageEntity();
        modelBuilder.ConfigureInboxMessageEntity();

        return modelBuilder;
    }
}
