namespace HackthonSpurs.Application.Embeddings;

/// <summary>Identify a compatible vector space without provider connection details.</summary>
/// <param name="IsConfigured">Whether this deployment can compute embeddings.</param>
/// <param name="ModelId">Identifier of the active model.</param>
/// <param name="ProfileVersion">Operator-managed revision of this vector space.</param>
/// <param name="VectorDimensions">Number of components in every vector.</param>
public sealed record EmbeddingProfile(bool IsConfigured, string ModelId, string ProfileVersion, int VectorDimensions);
