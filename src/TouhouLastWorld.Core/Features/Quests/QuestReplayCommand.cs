namespace TouhouLastWorld;

public sealed record QuestReplayCommand(
    global::TouhouLastWorld.LocalReplayMeasure? ReplayMeasure = null,
    global::System.String? Text = null);
