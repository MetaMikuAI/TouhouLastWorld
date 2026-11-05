namespace TouhouLastWorld;

public sealed record EndDispatchCommand(
    global::TouhouLastWorld.LocalDispatchSettlement? DispatchSettlement = null,
    global::System.Int64? EntityId = null);
