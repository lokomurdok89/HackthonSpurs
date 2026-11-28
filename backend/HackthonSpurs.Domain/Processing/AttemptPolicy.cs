namespace HackthonSpurs.Domain.Processing;

public static class AttemptPolicy
{
    /// <summary>Retry only eligible, classified transient failures below the attempt limit.</summary>
    /// <param name="retryEnabled">Whether the current job lane allows replay of external work.</param>
    /// <param name="transient">Whether the failure admits another attempt.</param>
    /// <param name="attempt">Number of attempts already claimed, including this one.</param>
    /// <param name="limit">Maximum permitted attempts for this lane.</param>
    /// <param name="firstBackoff">Delay after the first failed attempt.</param>
    /// <param name="laterBackoff">Delay after a subsequent failed attempt.</param>
    public static TimeSpan? RetryDelay(bool retryEnabled, bool transient, int attempt, int limit,
        TimeSpan firstBackoff, TimeSpan laterBackoff) =>
        retryEnabled && transient && attempt < limit ? attempt == 1 ? firstBackoff : laterBackoff : null;
}
