namespace MassTransit.PostgresOutbox;

internal static class RetryDelayCalculator
{
   public static DateTime ComputeNextRetryAt(int currentRetryCount, Settings settings)
   {
      ArgumentOutOfRangeException.ThrowIfNegative(currentRetryCount);
      ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(currentRetryCount, settings.InboxRetryIntervals.Length);

      var delay = settings.InboxRetryIntervals[currentRetryCount];

      return DateTime.UtcNow.Add(delay);
   }
}
