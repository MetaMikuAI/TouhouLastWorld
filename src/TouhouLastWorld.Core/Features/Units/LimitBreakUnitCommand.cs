namespace TouhouLastWorld;

public sealed record LimitBreakUnitCommand(
    global::System.Int64? EntityId = null,
    global::System.Int64[]? Ids = null,
    global::TouhouLastWorld.LocalLimitBreak? LimitBreak = null,
    global::System.Int32? Value = null);
