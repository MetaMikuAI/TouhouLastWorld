namespace TouhouLastWorld;

public sealed record StartDispatchCommand(
    global::TouhouLastWorld.LocalDispatchStart? DispatchStart = null,
    global::System.Int64? EntityId = null,
    global::System.Int64[]? Ids = null);
