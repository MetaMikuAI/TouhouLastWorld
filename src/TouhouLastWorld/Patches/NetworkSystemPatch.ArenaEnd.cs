namespace TouhouLastWorld.Patches;

public partial class NetworkSystemPatch
{
    private static LocalArguments? ReadArenaEnd(ArenaBattleEndRequest? request)
    {
        var end = request?.battle_end;
        if (end == null) return null;
        var score = request!.battle_score;
        var master = MasterDB.This();
        return new(ArenaEnd: new(end.result, request.battle_number,
            end.defeated_unit_id_list1?.ToArray().Select(id => id).ToArray() ?? [],
            (end.defeated_unit_id_list2?.Count ?? 0) != 0 || (end.defeated_unit_id_list3?.Count ?? 0) != 0,
            score == null ? null : new(score.attackCount, score.grazeCount, score.total_score,
                score.attack_count_score, score.graze_count_score, score.defeated_score),
            master?.ArenaWeeklyReward?.ToList()?.ToArray().Select(row => new LocalArenaReward(row.id, row.score, row.value)).ToArray(),
            master?.Setting?.ARENA_POINT_MAX ?? 0));
    }
}
