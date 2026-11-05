using System.Globalization;
using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalMissionRule(int Id, int Type, int EventId, int Required, bool Active, LocalInventoryReward[] Rewards);
public sealed record LocalMissionClaim(int Id, int All, int Type, int EventId, int ChapterId, LocalMissionRule[] Rules);
public sealed record LocalStoryMission(int Id, int EpisodeId, string Name, int DisplayOrder, (int Id, int Count)[] RewardGroups,
    int Type = 1, int BattleId = 0);
public sealed record LocalDailyQuestMission(int Id, string Name, int DisplayOrder, (int Id, int Count)[] RewardGroups,
    LocalDailyQuestDay Day, int SwitchingHour);
public sealed record LocalDailyLoginMission(int Id, string Name, int DisplayOrder, (int Id, int Count)[] RewardGroups,
    int SwitchingHour, DateTime Now)
{
    public DateTime DayStart => Now.AddHours(-SwitchingHour).Date.AddHours(SwitchingHour);
}

public sealed partial class OfflineServer
{
    internal const int DailySweepRewardType = 104;
    internal const int DailyQuestMissionType = 8;
    internal const int NoviceMissionType = 10;
    private const string DailyQuestMissionClaim = "_local_daily_quest_mission_claimed_on";

    private LocalReply MissionOverview(LocalDailyLoginMission? daily, LocalStoryMission[]? story, LocalDailyQuestMission? dailyQuest,
        LocalBattlePassPeriod? period)
    {
        var body = QueryTemplates["mission_top"].DeepClone().AsObject();
        if (OpenBattlePassPeriod(period) is { } pass)
            body["user_update"] = new JsonObject { ["battle_pass"] = pass };
        var overview = BuildMissionLists(daily, story, dailyQuest);
        body["mission"] = overview;
        var rows = overview["unlock_list"]!.AsArray().Concat(overview["clear_list"]!.AsArray()).ToArray();
        foreach (var field in new[] { "event_id", "chapter_id" })
            body[field + "_list"] = new JsonArray(rows.Select(row => row?[field]?.GetValue<int>() ?? 0)
                .Where(id => id > 0).Distinct().Select(id => (JsonNode?)JsonValue.Create(id)).ToArray());
        return new(0, body);
    }

    private JsonObject BuildMissionLists(LocalDailyLoginMission? daily, LocalStoryMission[]? story, LocalDailyQuestMission? dailyQuest)
    {
        var saved = user["_local_missions"] as JsonObject;
        var overview = (saved ?? QueryTemplates["mission_top"]["mission"]!.AsObject()).DeepClone().AsObject();
        var login = DailyLoginRow(daily, overview);
        if (login is not null)
            overview[login["current_value"]!.GetValue<int>() == 1 ? "clear_list" : "unlock_list"]!.AsArray().Add(login);
        var quest = DailyQuestMissionRow(dailyQuest, overview);
        if (quest is not null)
            overview[quest["current_value"]!.GetValue<int>() == 1 ? "clear_list" : "unlock_list"]!.AsArray().Add(quest);
        foreach (var row in StoryMissionRows(story, overview))
            overview[row["current_value"]!.GetValue<int>() == 1 ? "clear_list" : "unlock_list"]!.AsArray().Add(row);
        return overview;
    }

    private IEnumerable<JsonObject> StoryMissionRows(LocalStoryMission[]? story, JsonObject missions)
    {
        if (story is null) yield break;
        var displayed = missions["unlock_list"]!.AsArray().Concat(missions["clear_list"]!.AsArray())
            .Select(row => row?["id"]?.GetValue<int>() ?? 0).ToHashSet();
        var received = (user["_local_received_missions"] as JsonArray)?.Select(id => id!.GetValue<int>()).ToHashSet() ?? [];
        var episodes = (user["quest_playdata"] as JsonArray)?.ToLookup(row => row?["episode_id"]?.GetValue<int>() ?? 0);
        foreach (var mission in story)
        {
            if (received.Contains(mission.Id) || !displayed.Add(mission.Id)) continue;
            var battle = mission.BattleId > 0;
            var completed = episodes?[mission.EpisodeId].Any(episode => battle
                ? episode?["normal"]?["status"]?.GetValue<int>() is >= 2
                : episode?["scenario_status"]?.GetValue<int>() == 1) == true;
            var row = new JsonObject
            {
                ["id"] = mission.Id, ["type"] = mission.Type, ["name"] = mission.Name, ["complete_type"] = battle ? 4 : 3,
                ["complete_id"] = battle ? mission.BattleId : mission.EpisodeId, ["complete_value"] = 1, ["display_order"] = mission.DisplayOrder,
                ["open_mission_id"] = 0, ["event_id"] = 0, ["chapter_id"] = 0,
                ["current_value"] = completed ? 1 : 0, ["start_at"] = "", ["end_at"] = ""
            };
            SetMissionRewardGroups(row, mission.RewardGroups);
            yield return row;
        }
    }

    private JsonArray NoviceMissionBadge(LocalStoryMission[]? story) => new(
        BuildMissionLists(null, story, null)["clear_list"]!.AsArray()
            .Where(row => row?["type"]?.GetValue<int>() == NoviceMissionType)
            .OrderBy(row => row!["display_order"]?.GetValue<int>() ?? 0)
            .Select(row => row!["id"]!.DeepClone()).ToArray());

    private JsonObject? DailyLoginRow(LocalDailyLoginMission? daily, JsonObject missions)
    {
        if (daily is null || daily.Id <= 0 || daily.SwitchingHour is < 0 or > 23 || daily.RewardGroups.Length != 4)
            return null;
        var day = daily.DayStart;
        if (DateOnly.TryParseExact(user["_local_daily_login_mission_claimed_on"]?.GetValue<string>(), "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var claimed)
            && claimed >= DateOnly.FromDateTime(day))
            return null;
        if (missions["unlock_list"]!.AsArray().Concat(missions["clear_list"]!.AsArray())
                .Any(row => row?["id"]?.GetValue<int>() == daily.Id)
            || user["_local_received_missions"]?.AsArray().Any(id => id?.GetValue<int>() == daily.Id) == true)
            return null;
        var login = new JsonObject
        {
            ["id"] = daily.Id, ["type"] = 3, ["name"] = daily.Name, ["complete_type"] = 1, ["complete_id"] = 0,
            ["complete_value"] = 1, ["display_order"] = daily.DisplayOrder, ["open_mission_id"] = 0,
            ["event_id"] = 0, ["chapter_id"] = 0,
            ["current_value"] = lastLoginAt >= day && lastLoginAt <= daily.Now ? 1 : 0,
            ["start_at"] = day.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            ["end_at"] = day.AddDays(1).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
        };
        SetMissionRewardGroups(login, daily.RewardGroups);
        return login;
    }

    private static void SetMissionRewardGroups(JsonObject row, (int Id, int Count)[] groups)
    {
        for (var index = 0; index < groups.Length; index++)
        {
            var reward = groups[index];
            row[$"reward_group{index + 1}_id"] = reward.Id;
            row[$"reward_group{index + 1}_value"] = reward.Count;
        }
    }

    private JsonObject? DailyQuestMissionRow(LocalDailyQuestMission? definition, JsonObject missions)
    {
        if (definition is null || definition.Id <= 0 || definition.Day.StartAt(definition.SwitchingHour) is not { } start
            || definition.RewardGroups.Length != 4
            || definition.Day.Date != user[DailyQuestDay]?.GetValue<int>()
            || definition.Day.Date <= (user[DailyQuestMissionClaim]?.GetValue<int>() ?? 0)
            || missions["unlock_list"]!.AsArray().Concat(missions["clear_list"]!.AsArray())
                .Any(row => row?["id"]?.GetValue<int>() == definition.Id))
            return null;
        var won = HasDailyQuestWin(definition.Day);
        var row = new JsonObject
        {
            ["id"] = definition.Id, ["type"] = 3, ["name"] = definition.Name, ["complete_type"] = DailyQuestMissionType,
            ["complete_id"] = 0, ["complete_value"] = 1, ["display_order"] = definition.DisplayOrder,
            ["open_mission_id"] = 0, ["event_id"] = 0, ["chapter_id"] = 0, ["current_value"] = won ? 1 : 0,
            ["start_at"] = start.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            ["end_at"] = start.AddDays(1).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
        };
        SetMissionRewardGroups(row, definition.RewardGroups);
        return row;
    }

    private LocalReply ReceiveMission(LocalMissionClaim? request, LocalDailyLoginMission? daily, LocalStoryMission[]? story,
        LocalDailyQuestMission? dailyQuest)
    {
        if (request is null || request.All is < 0 or > 1 || request.Type < 0 || request.EventId < 0 || request.ChapterId < 0
            || (request.All == 0 ? request.Id <= 0 : request.Id != 0))
            return new(400, null, "Invalid mission claim.");
        var updated = user.DeepClone().AsObject();
        var missions = updated["_local_missions"] as JsonObject;
        if (missions is null && daily is null && story is null && dailyQuest is null)
            return new(404, null, "Mission state unavailable.");
        missions ??= QueryTemplates["mission_top"]["mission"]!.DeepClone().AsObject();
        var ready = missions["clear_list"] as JsonArray;
        if (ready is null || missions["unlock_list"] is not JsonArray)
            return new(404, null, "Mission state unavailable.");
        var login = DailyLoginRow(daily, missions);
        var quest = DailyQuestMissionRow(dailyQuest, missions);
        var claimable = ready.Concat(StoryMissionRows(story, missions).Where(row => row["current_value"]!.GetValue<int>() == 1));
        if (login?["current_value"]?.GetValue<int>() == 1) claimable = claimable.Append(login);
        if (quest?["current_value"]?.GetValue<int>() == 1) claimable = claimable.Append(quest);
        var selected = claimable.Where(row => row is JsonObject
            && (request.All == 1 || row["id"]?.GetValue<int>() == request.Id)
            && (request.Type == 0 || row["type"]?.GetValue<int>() == request.Type)
            && (request.EventId == 0 || row["event_id"]?.GetValue<int>() == request.EventId)
            && (request.ChapterId == 0 || row["chapter_id"]?.GetValue<int>() == request.ChapterId)).ToArray();
        if (selected.Length == 0 && request.All == 0) return new(404, null, "Mission is not claimable.");
        var received = updated["_local_received_missions"] as JsonArray ?? new JsonArray();
        if (updated["_local_received_missions"] is null) updated["_local_received_missions"] = received;
        var ids = new HashSet<int>();
        var rewards = new JsonArray();
        var receipts = new JsonArray();
        foreach (var mission in selected)
        {
            var id = mission!["id"]?.GetValue<int>() ?? 0;
            var rule = request.Rules.SingleOrDefault(row => row.Id == id);
            var isQuestMission = id == dailyQuest?.Id;
            if (id <= 0 || !ids.Add(id) || !isQuestMission && received.Any(entry => entry?.GetValue<int>() == id))
                return new(409, null, "Mission already received or duplicated.");
            if (rule is null || !rule.Active || rule.Required <= 0 || rule.Rewards.Length == 0
                || mission["type"]?.GetValue<int>() != rule.Type || mission["event_id"]?.GetValue<int>() != rule.EventId
                || mission["complete_value"]?.GetValue<int>() != rule.Required
                || mission["current_value"]?.GetValue<int>() is not int current || current < rule.Required)
                return new(503, null, "Mission progress or reward definition unavailable.");
            var objects = new JsonArray();
            foreach (var reward in rule.Rewards)
            {
                var error = ApplyMissionReward(updated, reward);
                if (error is not null) return error;
                var item = new JsonObject { ["object_type"] = reward.Type, ["object_id"] = reward.Id,
                    ["object_value"] = reward.Count, ["custom_parameter"] = "" };
                objects.Add(item.DeepClone());
                item["is_eff"] = false;
                rewards.Add(item);
            }
            receipts.Add(new JsonObject { ["mission_id"] = id, ["object_info"] = objects });
            if (ReferenceEquals(mission, login))
                updated["_local_daily_login_mission_claimed_on"] = daily!.DayStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            else if (isQuestMission)
            {
                updated[DailyQuestMissionClaim] = dailyQuest!.Day.Date;
                ready.Remove(mission);
            }
            else
            {
                received.Add(id);
                ready.Remove(mission);
            }
        }
        if (selected.Length > 0) Save(updated);
        var userUpdate = new JsonObject { ["gold"] = updated["gold"]!.DeepClone(), ["crystal"] = updated["crystal"]!.DeepClone(),
            ["item"] = updated["item"]!.DeepClone() };
        if (rewards.Any(reward => reward?["object_type"]?.GetValue<int>() == 2))
            userUpdate["phantasm_point"] = updated["phantasm_point"]!.DeepClone();
        if (updated["mop_up_day_coupon"] is JsonNode coupons)
            userUpdate["mop_up_day_coupon"] = coupons.DeepClone();
        return new(0, new() { ["mission"] = BuildMissionLists(daily, story, dailyQuest), ["reward_list"] = rewards,
            ["receive_mission_list"] = receipts, ["not_receive_message"] = "",
            ["user_update"] = userUpdate });
    }

    private static LocalReply? ApplyMissionReward(JsonObject updated, LocalInventoryReward reward) => reward.Type switch
    {
        2 => ApplyMissionSpiritReward(updated, reward),
        DailySweepRewardType => ApplyDailySweepReward(updated, reward),
        _ => ApplyInventoryReward(updated, reward)
    };

    private static LocalReply? ApplyMissionSpiritReward(JsonObject updated, LocalInventoryReward reward)
    {
        if (reward.Id != 0 || reward.Count <= 0 || reward.Capacity <= 0)
            return new(503, null, "Mission spirit point definition unsupported.");
        var points = updated["phantasm_point"]!;
        var total = (long)points["points"]!.GetValue<int>() + reward.Count;
        if (total > reward.Capacity) return new(409, null, "Mission spirit point reward exceeds capacity.");
        points["points"] = (int)total;
        return null;
    }

    private static LocalReply? ApplyDailySweepReward(JsonObject updated, LocalInventoryReward reward)
    {
        if (reward.Id != 0 || reward.Count <= 0)
            return new(503, null, "Daily sweep coupon definition unsupported.");
        var total = (long)(updated["mop_up_day_coupon"]?.GetValue<int>() ?? 0) + reward.Count;
        if (total > reward.Capacity) return new(409, null, "Daily sweep coupon reward exceeds capacity.");
        updated["mop_up_day_coupon"] = (int)total;
        return null;
    }
}
