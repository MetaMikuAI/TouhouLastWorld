namespace TouhouLastWorld;

public sealed record HistoryPlayerCommand(
    global::TouhouLastWorld.LocalHistoryDefinitions? HistoryDefinitions = null,
    global::System.String? Text = null);
