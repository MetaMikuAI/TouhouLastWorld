using System.Text.Json;

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
            master?.Setting?.ARENA_POINT_MAX ?? 0,
            end.result == 1 && Plugin.Server.PendingArenaCategory == 1 ? ReadDailyArenaRewards() : null));
    }

    private static LocalArenaDaily? ReadDailyArenaRewards()
    {
        var master = MasterDB.This();
        var setting = master?.Setting;
        var fixedRewards = master?.ArenaDailyConfirmReward?.ToList()?.ToArray();
        var rotating = master?.ArenaDailyRotateReward?.ToList()?.ToArray();
        var day = Plugin.Server.DailyArenaRewardDay;
        if (setting == null || fixedRewards == null || rotating == null || day < 0) return null;
        LocalInventoryReward Reward(int type, int id, int count) => new(type, id, count, type switch {
            3 => setting.GOLD_MAX, 6 => setting.CRYSTAL_MAX, 21 => setting.ARENA_POINT_MAX,
            12 when master?.Item?.Find(id) != null => setting.ITEM_MAX, _ => 0 });
        var stages = new List<LocalArenaDailyReward>();
        foreach (var row in fixedRewards)
        {
            if (string.IsNullOrWhiteSpace(row.rotate_reward_no_list)) return null;
            int[]? rotation;
            try { rotation = JsonSerializer.Deserialize<int[]>(row.rotate_reward_no_list); }
            catch (JsonException) { return null; }
            if (rotation == null) return null;
            var rewards = new List<LocalInventoryReward> { Reward(row.object_type, row.object_id, row.object_value) };
            var complete = true;
            foreach (var number in rotation)
            {
                var matches = rotating.Where(entry => entry.day_count == day && entry.rotate_no == number).ToArray();
                if (matches.Length != 1) { complete = false; break; }
                var entry = matches[0];
                rewards.Add(Reward(entry.object_type, entry.object_id, entry.object_value));
            }
            stages.Add(new(row.difficulty, complete ? rewards.ToArray() : null));
        }
        return new(day, stages.ToArray());
    }

}
