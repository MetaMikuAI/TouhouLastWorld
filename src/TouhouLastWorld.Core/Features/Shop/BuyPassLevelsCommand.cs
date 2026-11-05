namespace TouhouLastWorld;

public sealed record BuyPassLevelsCommand(
    global::TouhouLastWorld.LocalPassReward[]? PassRewards = null,
    global::System.Int32? Value = null);
