namespace TouhouLastWorld;

public sealed record IncreaseDestinyCommand(
    global::TouhouLastWorld.LocalDestinyGrowth? DestinyGrowth = null,
    global::System.Int64? EntityId = null,
    global::System.Int64[]? Ids = null);
