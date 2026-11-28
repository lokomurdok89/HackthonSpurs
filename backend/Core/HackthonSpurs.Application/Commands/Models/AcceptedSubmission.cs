using HackthonSpurs.Domain.Documents;

namespace HackthonSpurs.Application.Commands;

public sealed record AcceptedSubmission(Guid Id, SubmissionIdentity Identity);
