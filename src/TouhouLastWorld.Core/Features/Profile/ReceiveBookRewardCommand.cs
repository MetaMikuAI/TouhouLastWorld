namespace TouhouLastWorld;

public sealed record ReceiveBookRewardCommand(
    global::TouhouLastWorld.LocalBookReward? BookReward = null,
    global::System.Int32? Value = null);
