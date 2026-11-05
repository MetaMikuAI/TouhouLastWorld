namespace TouhouLastWorld;

public sealed record UnlockExtraStatCommand(
    global::System.Int64? EntityId = null,
    global::TouhouLastWorld.LocalStatUnlock? StatUnlock = null,
    global::System.Int32? Value = null);
