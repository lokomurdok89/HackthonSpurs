using HackthonSpurs.Application.Queries;
using HackthonSpurs.Resources;

namespace HackthonSpurs.Application.Commands;

public sealed record UserChange(UserView? User = null, BackendError? Error = null, bool CallerRevoked = false);
