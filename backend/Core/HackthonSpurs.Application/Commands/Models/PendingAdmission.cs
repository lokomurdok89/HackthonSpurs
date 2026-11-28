using HackthonSpurs.Domain.Documents;

namespace HackthonSpurs.Application.Commands;

public sealed record PendingAdmission(Guid Key, Guid Id, SubmissionIdentity Identity, string MediaType, long Length);
