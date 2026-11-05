namespace TouhouLastWorld;

public sealed record HistoryTeamCommand(
    global::TouhouLastWorld.LocalHistoryDefinitions? HistoryDefinitions = null,
    global::TouhouLastWorld.LocalHistoryQuery? HistoryQuery = null);
