namespace TouhouLastWorld;

public sealed record RecycleUnitsCommand(
    global::System.Int64[]? Ids = null,
    global::TouhouLastWorld.LocalUnitRecycle? UnitRecycle = null);
