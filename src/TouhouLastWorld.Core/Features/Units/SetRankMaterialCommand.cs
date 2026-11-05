namespace TouhouLastWorld;

public sealed record SetRankMaterialCommand(
    global::System.Int64? EntityId = null,
    global::System.Int64[]? Ids = null,
    global::TouhouLastWorld.LocalRankMaterial? RankMaterial = null,
    global::System.Int32? Value = null);
