using System.Globalization;
using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalMissionRule(int Id, int Type, int EventId, int Required, bool Active, LocalInventoryReward[] Rewards);
public sealed record LocalMissionClaim(int Id, int All, int Type, int EventId, int ChapterId, LocalMissionRule[] Rules);
public sealed record LocalStoryMission(int Id, int EpisodeId, string Name, int DisplayOrder, (int Id, int Count)[] RewardGroups);
public sealed record LocalDailyLoginMission(int Id, string Name, int DisplayOrder, (int Id, int Count)[] RewardGroups,
    int SwitchingHour, DateTime Now)
{
    public DateTime DayStart => Now.AddHours(-SwitchingHour).Date.AddHours(SwitchingHour);
}

public sealed partial class OfflineServer
{
    internal const int DailySweepRewardType = 104;

    private LocalReply MissionOverview(LocalDailyLoginMission? daily, LocalStoryMission[]? story)
    {
        var body = QueryTemplates["mission_top"].DeepClone().AsObject();
        var overview = BuildMissionLists(daily, story);
        body["mission"] = overview;
        var rows = overview["unlock_list"]!.AsArray().Concat(overview["clear_list"]!.AsArray()).ToArray();
        foreach (var field in new[] { "event_id", "chapter_id" })
            body[field + "_list"] = new JsonArray(rows.Select(row => row?[field]?.GetValue<int>() ?? 0)
                .Where(id => id > 0).Distinct().Select(id => (JsonNode?)JsonValue.Create(id)).ToArray());
        return new(0, body);
    }

    private JsonObject BuildMissionLists(LocalDailyLoginMission? daily, LocalStoryMission[]? story)
    {
        var saved = user["_local_missions"] as JsonObject;
        var overview = (saved ?? QueryTemplates["mission_top"]["mission"]!.AsObject()).DeepClone().AsObject();
        var login = DailyLoginRow(daily, overview);
        if (login is not null)
            overview[login["current_value"]!.GetValue<int>() == 1 ? "clear_list" : "unlock_list"]!.AsArray().Add(login);
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
        var completed = (user["quest_playdata"] as JsonArray)?.Where(row => row?["scenario_status"]?.GetValue<int>() == 1)
            .Select(row => row!["episode_id"]!.GetValue<int>()).ToHashSet() ?? [];
        foreach (var mission in story)
        {
            if (received.Contains(mission.Id) || !displayed.Add(mission.Id)) continue;
            var row = new JsonObject
            {
                ["id"] = mission.Id, ["type"] = 1, ["name"] = mission.Name, ["complete_type"] = 3,
                ["complete_id"] = mission.EpisodeId, ["complete_value"] = 1, ["display_order"] = mission.DisplayOrder,
                ["open_mission_id"] = 0, ["event_id"] = 0, ["chapter_id"] = 0,
                ["current_value"] = completed.Contains(mission.EpisodeId) ? 1 : 0, ["start_at"] = "", ["end_at"] = ""
            };
            SetMissionRewardGroups(row, mission.RewardGroups);
            yield return row;
        }
    }

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

    private LocalReply ReceiveMission(LocalMissionClaim? request, LocalDailyLoginMission? daily, LocalStoryMission[]? story)
    {
        if (request is null || request.All is < 0 or > 1 || request.Type < 0 || request.EventId < 0 || request.ChapterId < 0
            || (request.All == 0 ? request.Id <= 0 : request.Id != 0))
            return new(400, null, "Invalid mission claim.");
        var updated = user.DeepClone().AsObject();
        var missions = updated["_local_missions"] as JsonObject;
        if (missions is null && daily is null && story is null)
            return new(404, null, "Mission state unavailable.");
        missions ??= QueryTemplates["mission_top"]["mission"]!.DeepClone().AsObject();
        var ready = missions["clear_list"] as JsonArray;
        if (ready is null || missions["unlock_list"] is not JsonArray)
            return new(404, null, "Mission state unavailable.");
        var login = DailyLoginRow(daily, missions);
        var claimable = ready.Concat(StoryMissionRows(story, missions).Where(row => row["current_value"]!.GetValue<int>() == 1));
        if (login?["current_value"]?.GetValue<int>() == 1) claimable = claimable.Append(login);
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
            if (id <= 0 || !ids.Add(id) || received.Any(entry => entry?.GetValue<int>() == id))
                return new(409, null, "Mission already received or duplicated.");
            if (rule is null || !rule.Active || rule.Required <= 0 || rule.Rewards.Length == 0
                || mission["type"]?.GetValue<int>() != rule.Type || mission["event_id"]?.GetValue<int>() != rule.EventId
                || mission["complete_value"]?.GetValue<int>() != rule.Required
                || mission["current_value"]?.GetValue<int>() is not int current || current < rule.Required)
                return new(503, null, "Mission progress or reward definition unavailable.");
            var objects = new JsonArray();
            foreach (var reward in rule.Rewards)
            {
                var error = reward.Type == DailySweepRewardType
                    ? ApplyDailySweepReward(updated, reward) : ApplyInventoryReward(updated, reward);
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
            else
            {
                received.Add(id);
                ready.Remove(mission);
            }
        }
        if (selected.Length > 0) Save(updated);
        var userUpdate = new JsonObject { ["gold"] = updated["gold"]!.DeepClone(), ["crystal"] = updated["crystal"]!.DeepClone(),
            ["item"] = updated["item"]!.DeepClone() };
        if (updated["mop_up_day_coupon"] is JsonNode coupons)
            userUpdate["mop_up_day_coupon"] = coupons.DeepClone();
        return new(0, new() { ["mission"] = BuildMissionLists(daily, story), ["reward_list"] = rewards,
            ["receive_mission_list"] = receipts, ["not_receive_message"] = "",
            ["user_update"] = userUpdate });
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
