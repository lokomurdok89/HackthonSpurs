using HackthonSpurs.Domain.Processing;
using HackthonSpurs.Resources;

namespace HackthonSpurs.Application.Processing;

internal static class RetrySchedule
{
    /// <summary>Apply the shared bounded retry schedule to a classified failure.</summary>
    /// <param name="enabled">Whether the lane permits repeating work.</param>
    /// <param name="transient">Whether the failure can be retried.</param>
    /// <param name="attempt">Current lease attempt number.</param>
    public static TimeSpan? AfterFailure(bool enabled, bool transient, int attempt) =>
        AttemptPolicy.RetryDelay(enabled, transient, attempt, Policy.MaxAttempts,
            TimeSpan.FromSeconds(Policy.FirstBackoffSeconds), TimeSpan.FromSeconds(Policy.SecondBackoffSeconds));
}
