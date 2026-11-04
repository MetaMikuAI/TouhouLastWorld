using Applet;

namespace TouhouLastWorld.Patches;

public partial class NetworkSystemPatch
{
    private static LocalArguments? ReadBattlePassClaim(BattlePassMissionEndRequest? request)
    {
        if (request == null) return null;
        var master = MasterDB.This();
        var current = User.This()?.battle_pass;
        var pass = current == null ? null : master?.BattlePass?.Find(current.battle_pass_id);
        var tasks = master?.BattlePassMission?.ToList()?.ToArray();
        var levels = master?.BattlePassExp?.ToList()?.ToArray();
        var time = GameSystem.This()?.Time;
        if (pass == null || tasks == null || levels == null || time == null) return null;
        return new(BattlePassClaim: new(request.mission_id, request.is_all, pass.id,
            time.IsActiveDate(pass.start_at, pass.end_at),
            tasks.Where(row => row.battle_pass_id == pass.id && row.reward_group1_id == 0 && row.reward_group2_id == 0
                && row.reward_group3_id == 0 && row.reward_group4_id == 0)
                .Select(row => new LocalBattlePassTask(row.id, row.complete_value, row.reward_battle_pass_exp,
                    (string.IsNullOrWhiteSpace(row.start_at) && string.IsNullOrWhiteSpace(row.end_at))
                    || time.IsActiveDate(row.start_at, row.end_at))).ToArray(),
            levels.Where(row => row.exp_group_id == pass.exp_group_id).ToDictionary(row => row.level, row => row.exp)));
    }

    private static LocalArguments? ReadMissionClaim(MissionEndRequest? request)
    {
        if (request == null) return null;
        var master = MasterDB.This();
        var definitions = master?.Mission?.ToList()?.ToArray();
        var rewards = master?.MissionReward?.ToList()?.ToArray();
        var setting = master?.Setting;
        var time = GameSystem.This()?.Time;
        if (definitions == null || rewards == null || setting == null || time == null) return null;
        var rules = new List<LocalMissionRule>();
        foreach (var definition in definitions)
        {
            if (request.is_all == 0 && definition.id != request.mission_id) continue;
            var grants = new List<LocalInventoryReward>();
            var supported = true;
            foreach (var group in new[] { definition.reward_group1_id, definition.reward_group2_id,
                definition.reward_group3_id, definition.reward_group4_id }.Where(id => id > 0))
            {
                var entries = rewards.Where(row => row.mission_reward_group_id == group).ToArray();
                if (entries.Length != 1 || !string.IsNullOrEmpty(entries[0].custom_parameter))
                {
                    supported = false;
                    break;
                }
                var reward = entries[0];
                grants.Add(new(reward.object_type, reward.object_id, reward.object_value, reward.object_type switch
                {
                    3 => setting.GOLD_MAX, 6 => setting.CRYSTAL_MAX,
                    12 when master?.Item?.Find(reward.object_id) != null => setting.ITEM_MAX, _ => 0
                }));
            }
            if (supported)
                rules.Add(new(definition.id, definition.type, definition.event_id, definition.complete_value,
                    (string.IsNullOrWhiteSpace(definition.start_at) && string.IsNullOrWhiteSpace(definition.end_at))
                        || time.IsActiveDate(definition.start_at, definition.end_at), grants.ToArray()));
        }
        return new(MissionClaim: new(request.mission_id, request.is_all, request.mission_type, request.event_id, request.chapter_id, rules.ToArray()));
    }
}
