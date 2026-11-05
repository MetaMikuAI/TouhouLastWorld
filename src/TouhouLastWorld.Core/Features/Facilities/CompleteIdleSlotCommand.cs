namespace TouhouLastWorld;

public sealed record CompleteIdleSlotCommand(
    global::System.Int64? EntityId = null,
    global::TouhouLastWorld.LocalIdleSet? Idle = null,
    global::System.Int32? Value = null);
