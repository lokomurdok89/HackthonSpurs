using HackthonSpurs.Resources;

namespace HackthonSpurs.Application.Processing;

internal static class RetrySchedule
{
    /// <summary>Retry eligible transient failures below the shared attempt limit.</summary>
    /// <param name="enabled">Whether the lane permits repeating work.</param>
    /// <param name="transient">Whether the failure can be retried.</param>
    /// <param name="attempt">Number of attempts already claimed, including this one.</param>
    public static TimeSpan? AfterFailure(bool enabled, bool transient, int attempt)
    {
        if (!enabled || !transient || attempt >= Policy.MaxAttempts) return null;
        return TimeSpan.FromSeconds(attempt == 1 ? Policy.FirstBackoffSeconds : Policy.SecondBackoffSeconds);
    }
}
