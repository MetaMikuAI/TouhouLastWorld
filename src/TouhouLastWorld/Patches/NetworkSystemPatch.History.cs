namespace TouhouLastWorld.Patches;

public partial class NetworkSystemPatch
{
    private static LocalArguments? ReadRankingHistory(SeriesBattleRankingGetTeamInfoRequest? request)
    {
        if (request == null) return null;
        var master = MasterDB.This();
        var units = master?.Unit?.ToList()?.ToArray();
        var costumes = master?.Costume?.ToList()?.ToArray();
        var groups = master?.SeriesBattleStageGroup?.ToList()?.ToArray();
        return new(Value: request.manage_id, Text: request.public_id,
            HistoryDefinitions: units == null || costumes == null || groups == null ? null
                : new(units.Select(row => row.id).ToArray(), costumes.ToDictionary(row => row.id, row => row.unit_id),
                    Array.Empty<int>(), StageGroups: groups.Select(row => row.id).ToArray()));
    }

    private static LocalArguments ReadHistoryPlayer(string? playerId)
    {
        var master = MasterDB.This();
        var units = master?.Unit?.ToList()?.ToArray();
        var costumes = master?.Costume?.ToList()?.ToArray();
        var achievements = master?.Achievement?.ToList()?.ToArray();
        return new LocalArguments(Text: playerId, HistoryDefinitions: units == null || costumes == null || achievements == null ? null
            : new(units.Select(row => row.id).ToArray(), costumes.ToDictionary(row => row.id, row => row.unit_id),
                Array.Empty<int>(), achievements.Select(row => row.id).ToArray()));
    }
}
