namespace HackthonSpurs.Application.Commands;

/// <summary>Request admission of one bounded original under an idempotency key.</summary>
/// <param name="Key">Client operation key reused for retries.</param>
/// <param name="Original">Staged upload, independent of how its bytes are stored.</param>
/// <param name="DeclaredType">Media type declared by the caller.</param>
/// <param name="Spur">Whether external image reading was explicitly requested.</param>
public sealed record AcceptDocument(Guid Key, IStagedOriginal Original, string DeclaredType, bool Spur);
