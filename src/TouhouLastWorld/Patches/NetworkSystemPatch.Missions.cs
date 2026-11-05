using Applet;

namespace TouhouLastWorld.Patches;

public partial class NetworkSystemPatch
{
    private static LocalArguments? ReadMissionOverview()
    {
        var master = MasterDB.This();
        var definitions = master?.Mission?.ToList()?.ToArray();
        var rewards = master?.MissionReward?.ToList()?.ToArray().ToLookup(row => row.mission_reward_group_id);
        var setting = master?.Setting;
        var day = ReadDailyQuestDay();
        return definitions == null || rewards == null || setting == null ? null
            : new(DailyLoginMission: ReadDailyLoginMission(definitions, rewards, setting.GAME_DATE_SWITCHING_HOUR),
                StoryMissions: ReadStoryMissions(master!, definitions, rewards), DailyQuestDay: day,
                DailyQuestMission: ReadDailyQuestMission(definitions, rewards, day, setting.GAME_DATE_SWITCHING_HOUR),
                BattlePassPeriod: ReadBattlePassPeriod(master!));
    }

    private static LocalBattlePassPeriod? ReadBattlePassPeriod(MasterDB master)
    {
        var panel = FindActivePanel<MissionBehavior>();
        if (panel?.m_battlePassPanel == null || panel.m_battlePassMissionToggle == null) return null;
        var periods = master.BattlePass?.ToList()?.ToArray().Where(row => row.IsBeginHeld()).ToArray();
        var existingId = User.This()?.battle_pass?.battle_pass_id;
        var period = periods?.FirstOrDefault(row => row.id == existingId);
        if (period == null && periods is { Length: 1 }) period = periods[0];
        if (period == null) return null;
        var initial = master.BattlePassExp?.ToList()?.ToArray()
            .Where(row => row.exp_group_id == period.exp_group_id && row.exp == 0).Take(2).ToArray();
        return initial is { Length: 1 } ? new(period.id, initial[0].level) : null;
    }

    private static LocalDailyLoginMission? ReadDailyLoginMission(MissionRecord[] definitions, ILookup<int, MissionRewardRecord> rewards,
        int switchingHour)
    {
        var daily = definitions.Where(row => row.type == MissionBehavior.c_menu_daily
            && row.complete_type == 1 && row.complete_id == 0 && row.complete_value == 1 && row.event_id == 0
            && string.IsNullOrWhiteSpace(row.start_at) && string.IsNullOrWhiteSpace(row.end_at)).Take(2).ToArray();
        if (daily.Length != 1) return null;
        var row = daily[0];
        var fixedRewards = ReadFixedMissionRewards(row, rewards);
        return fixedRewards is null ? null
            : new(row.id, row.name, row.display_order, MissionRewardDisplay(fixedRewards), switchingHour, DateTime.Now);
    }

    private static LocalDailyQuestMission? ReadDailyQuestMission(MissionRecord[] definitions,
        ILookup<int, MissionRewardRecord> rewards, LocalDailyQuestDay? day, int switchingHour)
    {
        if (day is null) return null;
        var daily = definitions.Where(row => row.type == MissionBehavior.c_menu_daily
            && row.complete_type == OfflineServer.DailyQuestMissionType && row.complete_id == 0 && row.complete_value == 1
            && row.event_id == 0
            && string.IsNullOrWhiteSpace(row.start_at) && string.IsNullOrWhiteSpace(row.end_at)).Take(2).ToArray();
        if (daily.Length != 1) return null;
        var row = daily[0];
        var groups = ReadFixedMissionRewards(row, rewards);
        return groups is null || !groups.Any(group => group.Reward != null) ? null
            : new(row.id, row.name, row.display_order, MissionRewardDisplay(groups), day, switchingHour);
    }

    private static LocalStoryMission[] ReadStoryMissions(MasterDB master, MissionRecord[] definitions, ILookup<int, MissionRewardRecord> rewards)
    {
        var progress = User.This()?.playdata;
        if (progress == null) return [];
        var missions = new List<LocalStoryMission>();
        foreach (var row in definitions)
        {
            if (row.type != MissionBehavior.c_menu_main || row.complete_type != 3 || row.complete_value != 1 || row.event_id != 0
                || !string.IsNullOrWhiteSpace(row.start_at) || !string.IsNullOrWhiteSpace(row.end_at))
                continue;
            var episode = master.Episode?.Find(row.complete_id);
            if (episode == null || episode.type != 1 || master.Chapter?.Find(episode.chapter_id)?.type != 1
                || progress.GetEpisodeInfo(episode, PlayData.Difficulty.Normal)?.state is not >= PlayData.State.New)
                continue;
            var groups = ReadFixedMissionRewards(row, rewards);
            if (groups is null || !groups.Any(group => group.Reward != null)
                || groups.Any(group => group.Reward is { } reward && (reward.object_type != 6 || reward.object_id != 0)))
                continue;
            missions.Add(new(row.id, episode.id, row.name, row.display_order, MissionRewardDisplay(groups)));
        }
        return missions.ToArray();
    }

    private static (int Id, int Count)[] MissionRewardDisplay((int Id, MissionRewardRecord? Reward)[] rewards) =>
        rewards.Select(group => (group.Id, group.Reward?.object_value ?? 0)).ToArray();

    private static (int Id, MissionRewardRecord? Reward)[]? ReadFixedMissionRewards(MissionRecord row,
        ILookup<int, MissionRewardRecord> rewards)
    {
        var groups = new[] { row.reward_group1_id, row.reward_group2_id, row.reward_group3_id, row.reward_group4_id };
        var fixedRewards = new (int Id, MissionRewardRecord? Reward)[groups.Length];
        for (var index = 0; index < groups.Length; index++)
        {
            var id = groups[index];
            if (id == 0) continue;
            var entries = rewards[id].Take(2).ToArray();
            if (entries.Length != 1 || entries[0].object_value <= 0 || !string.IsNullOrEmpty(entries[0].custom_parameter))
                return null;
            fixedRewards[index] = (id, entries[0]);
        }
        return fixedRewards;
    }

    private static LocalArguments? ReadBattlePassRewards()
    {
        var master = MasterDB.This();
        var current = User.This()?.battle_pass;
        var pass = current == null ? null : master?.BattlePass?.Find(current.battle_pass_id);
        var levels = master?.BattlePassLevelReward?.ToList()?.ToArray();
        var rewards = master?.BattlePassReward?.ToList()?.ToArray();
        var setting = master?.Setting;
        var time = GameSystem.This()?.Time;
        if (pass == null || levels == null || rewards == null || setting == null || time == null) return null;
        LocalInventoryReward[]? Group(int id)
        {
            if (id == 0) return Array.Empty<LocalInventoryReward>();
            var entries = rewards.Where(row => row.battle_pass_reward_group_id == id).ToArray();
            if (entries.Length != 1 || entries[0].custom_parameter != 0) return null;
            var reward = entries[0];
            return new[] { new LocalInventoryReward(reward.object_type, reward.object_id, reward.object_value, reward.object_type switch
            {
                3 => setting.GOLD_MAX, 6 => setting.CRYSTAL_MAX,
                12 when master?.Item?.Find(reward.object_id) != null => setting.ITEM_MAX, _ => 0
            }) };
        }
        return new(BattlePassRewards: new(pass.id, time.IsActiveDate(pass.start_at, pass.end_at),
            levels.Where(row => row.reward_set_id == pass.reward_set_id).Select(row => new LocalBattlePassLevelReward(row.level,
                Group(row.normal_reward_group_id), Group(row.premium_reward_group_id), Group(row.premium_reward_group_id_2))).ToArray()));
    }

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
        var day = ReadDailyQuestDay();
        return new(BattlePassClaim: new(request.mission_id, request.is_all, pass.id,
            pass.IsBeginHeld(),
            tasks.Where(row => row.battle_pass_id == pass.id && row.reward_group1_id == 0 && row.reward_group2_id == 0
                && row.reward_group3_id == 0 && row.reward_group4_id == 0)
                .Select(row => new LocalBattlePassTask(row.id, row.complete_value, row.reward_battle_pass_exp,
                    (string.IsNullOrWhiteSpace(row.start_at) && string.IsNullOrWhiteSpace(row.end_at))
                    || time.IsActiveDate(row.start_at, row.end_at))).ToArray(),
            levels.Where(row => row.exp_group_id == pass.exp_group_id).ToDictionary(row => row.level, row => row.exp)),
            DailyQuestDay: day, BattlePassDaily: ReadBattlePassDaily(master!, pass, tasks, day));
    }

    private static LocalArguments? ReadBattlePassDailyOverview()
    {
        var master = MasterDB.This();
        var status = User.This()?.battle_pass;
        var pass = status == null ? null : master?.BattlePass?.Find(status.battle_pass_id);
        var tasks = master?.BattlePassMission?.ToList()?.ToArray();
        var day = ReadDailyQuestDay();
        return pass == null || tasks == null ? null
            : new(DailyQuestDay: day, BattlePassDaily: ReadBattlePassDaily(master!, pass, tasks, day));
    }

    private static LocalBattlePassDaily? ReadBattlePassDaily(MasterDB master, BattlePassRecord pass,
        BattlePassMissionRecord[] tasks, LocalDailyQuestDay? day)
    {
        if (day is null || !pass.IsBeginHeld() || master.Setting is not { } setting) return null;
        var rewards = master.BattlePassReward?.ToList()?.ToArray().ToLookup(reward => reward.battle_pass_reward_group_id);
        if (rewards is null) return null;
        var result = new List<LocalBattlePassDailyTask>();
        foreach (var task in tasks)
        {
            if (task.battle_pass_id != 0 || task.type != (int)BattlePassMissionEndRequest.MissionType.Daily
                || task.complete_type is not (1 or OfflineServer.BattleWinMissionType or OfflineServer.DailyQuestMissionType)
                || task.complete_id != 0 || task.complete_value <= 0 || task.event_id != 0 || task.chapter_id != 0
                || task.reward_battle_pass_exp <= 0
                || !string.IsNullOrWhiteSpace(task.start_at) || !string.IsNullOrWhiteSpace(task.end_at))
                continue;
            var groups = new[] { task.reward_group1_id, task.reward_group2_id, task.reward_group3_id, task.reward_group4_id };
            var display = new (int Id, int Count)[groups.Length];
            var grants = new List<LocalInventoryReward>();
            for (var index = 0; index < groups.Length; index++)
            {
                var groupId = groups[index];
                if (groupId == 0) continue;
                var entries = rewards[groupId].Take(2).ToArray();
                if (entries.Length != 1 || entries[0].custom_parameter != 0 || entries[0].object_value <= 0) break;
                var reward = entries[0];
                var capacity = reward.object_type switch
                {
                    2 when reward.object_id == 0 => setting.PHANTASM_POINT_MAX,
                    3 when reward.object_id == 0 => setting.GOLD_MAX,
                    6 when reward.object_id == 0 => setting.CRYSTAL_MAX,
                    12 when master.Item?.Find(reward.object_id) != null => setting.ITEM_MAX,
                    OfflineServer.DailySweepRewardType when reward.object_id == 0 => int.MaxValue,
                    _ => 0
                };
                if (capacity <= 0) break;
                display[index] = (groupId, reward.object_value);
                grants.Add(new(reward.object_type, reward.object_id, reward.object_value, capacity));
            }
            if (grants.Count != groups.Count(id => id != 0)) continue;
            result.Add(new(new(task.id, task.complete_value, task.reward_battle_pass_exp, true, grants.ToArray()),
                task.complete_type, task.name, task.display_order, display));
        }
        return new(pass.id, day, setting.GAME_DATE_SWITCHING_HOUR, DateTime.Now, result.ToArray());
    }

    private static LocalArguments? ReadMissionClaim(MissionEndRequest? request)
    {
        if (request == null) return null;
        var master = MasterDB.This();
        var definitions = master?.Mission?.ToList()?.ToArray();
        var rewards = master?.MissionReward?.ToList()?.ToArray().ToLookup(row => row.mission_reward_group_id);
        var setting = master?.Setting;
        var time = GameSystem.This()?.Time;
        if (definitions == null || rewards == null || setting == null || time == null) return null;
        var rules = new List<LocalMissionRule>();
        foreach (var definition in definitions)
        {
            if (request.is_all == 0 && definition.id != request.mission_id) continue;
            var groups = ReadFixedMissionRewards(definition, rewards);
            if (groups is null) continue;
            var grants = groups.Select(group => group.Reward).OfType<MissionRewardRecord>()
                .Select(reward => new LocalInventoryReward(reward.object_type, reward.object_id, reward.object_value, reward.object_type switch
                {
                    2 => setting.PHANTASM_POINT_MAX,
                    3 => setting.GOLD_MAX, 6 => setting.CRYSTAL_MAX,
                    12 when master?.Item?.Find(reward.object_id) != null => setting.ITEM_MAX,
                    OfflineServer.DailySweepRewardType => int.MaxValue,
                    _ => 0
                })).ToArray();
            rules.Add(new(definition.id, definition.type, definition.event_id, definition.complete_value,
                (string.IsNullOrWhiteSpace(definition.start_at) && string.IsNullOrWhiteSpace(definition.end_at))
                    || time.IsActiveDate(definition.start_at, definition.end_at), grants));
        }
        var day = ReadDailyQuestDay();
        return new(MissionClaim: new(request.mission_id, request.is_all, request.mission_type, request.event_id, request.chapter_id, rules.ToArray()),
            DailyLoginMission: ReadDailyLoginMission(definitions, rewards, setting.GAME_DATE_SWITCHING_HOUR),
            StoryMissions: ReadStoryMissions(master!, definitions, rewards), DailyQuestDay: day,
            DailyQuestMission: ReadDailyQuestMission(definitions, rewards, day, setting.GAME_DATE_SWITCHING_HOUR));
    }
}
