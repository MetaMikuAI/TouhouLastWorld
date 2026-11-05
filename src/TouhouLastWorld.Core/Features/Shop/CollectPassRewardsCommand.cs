namespace TouhouLastWorld;

public sealed record CollectPassRewardsCommand(
    global::System.Int64? EntityId = null,
    global::TouhouLastWorld.LocalPassReward[]? PassRewards = null,
    global::System.String? Text = null,
    global::System.Int32? Value = null);
