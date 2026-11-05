namespace TouhouLastWorld;

public sealed record ClaimEventPointRewardCommand(
    global::TouhouLastWorld.LocalEventRewards? EventRewards = null,
    global::System.Int32? Value = null);
