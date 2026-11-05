namespace TouhouLastWorld;

public sealed record ShowPassRewardsCommand(
    global::TouhouLastWorld.LocalPassReward[]? PassRewards = null);
