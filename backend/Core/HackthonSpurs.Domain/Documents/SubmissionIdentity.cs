namespace HackthonSpurs.Domain.Documents;

/// <summary>Identify the content and reader lane selected by a document submission.</summary>
/// <param name="Sha256">Canonical SHA-256 digest of the accepted bytes.</param>
/// <param name="SendToOcr">Whether the original was explicitly submitted to OCR.</param>
public readonly record struct SubmissionIdentity(string Sha256, bool SendToOcr)
{
    /// <summary>Return whether the proposed submission replays this exact operation.</summary>
    /// <param name="proposed">Current content digest and processing mode, independent of filename.</param>
    public bool Matches(SubmissionIdentity proposed) =>
        string.Equals(Sha256, proposed.Sha256, StringComparison.Ordinal) && SendToOcr == proposed.SendToOcr;
}
