namespace TouhouLastWorld;

public sealed record PromoteRankCommand(
    global::System.Int64? EntityId = null,
    global::TouhouLastWorld.LocalRankPromotion? RankPromotion = null);
