namespace HackthonSpurs.Domain.Workspace;

public enum AdministratorTransition { Allowed, LastActiveAdministrator, SelfLockout }

public static class AdministratorPolicy
{
    /// <summary>Determine whether changing the target removes one current active administrator.</summary>
    /// <param name="wasActiveAdministrator">Target currently has active administrator access.</param>
    /// <param name="willBeActiveAdministrator">Target would retain active administrator access.</param>
    public static bool RemovesAccess(bool wasActiveAdministrator, bool willBeActiveAdministrator) =>
        wasActiveAdministrator && !willBeActiveAdministrator;

    /// <summary>Reject removal of the last active administrator before checking caller self-lockout.</summary>
    /// <param name="removesAccess">Whether the proposed change removes the target's administrator access.</param>
    /// <param name="hasOtherAdministrator">Whether another active administrator exists under the write lock.</param>
    /// <param name="isCaller">Whether the target is the signed-in administrator.</param>
    public static AdministratorTransition Decide(bool removesAccess, bool hasOtherAdministrator, bool isCaller) =>
        !removesAccess ? AdministratorTransition.Allowed :
        !hasOtherAdministrator ? AdministratorTransition.LastActiveAdministrator :
        isCaller ? AdministratorTransition.SelfLockout : AdministratorTransition.Allowed;
}
