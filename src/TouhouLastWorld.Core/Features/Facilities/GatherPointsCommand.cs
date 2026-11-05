namespace TouhouLastWorld;

public sealed record GatherPointsCommand(
    global::System.Int64? EntityId = null,
    global::TouhouLastWorld.LocalBoxIncome? SpiritIncome = null,
    global::System.Int32? Value = null);
