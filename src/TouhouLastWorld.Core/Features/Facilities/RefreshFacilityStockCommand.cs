namespace TouhouLastWorld;

public sealed record RefreshFacilityStockCommand(
    global::TouhouLastWorld.LocalBoxIncome? GoldIncome = null,
    global::TouhouLastWorld.LocalBoxIncome? SpiritIncome = null);
