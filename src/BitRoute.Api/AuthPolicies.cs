namespace BitRoute.Api;

/// <summary>Names of the authorization policies registered in Program.cs.</summary>
public static class AuthPolicies
{
    public const string AdminOnly = nameof(AdminOnly);
    public const string OperatorOnly = nameof(OperatorOnly);
}
