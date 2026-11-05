namespace TouhouLastWorld;

internal sealed class SessionState
{
    internal string? session;
    internal DateTime? lastLoginAt;
    internal LocalAccount? account;
    internal sealed record LocalAccount(string DeviceId, string UserId, string PublicId);
}
