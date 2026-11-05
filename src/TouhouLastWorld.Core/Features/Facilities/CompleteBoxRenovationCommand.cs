namespace TouhouLastWorld;

public sealed record CompleteBoxRenovationCommand(
    global::System.Int64? EntityId = null,
    global::TouhouLastWorld.LocalBoxIncome? GoldIncome = null,
    global::TouhouLastWorld.LocalBoxIncome? SpiritIncome = null,
    global::System.Int32? Value = null);
