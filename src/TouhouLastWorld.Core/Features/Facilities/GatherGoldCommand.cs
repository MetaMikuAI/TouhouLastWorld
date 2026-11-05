namespace TouhouLastWorld;

public sealed record GatherGoldCommand(
    global::System.Int64? EntityId = null,
    global::TouhouLastWorld.LocalBoxIncome? GoldIncome = null,
    global::System.Int32? Value = null);
