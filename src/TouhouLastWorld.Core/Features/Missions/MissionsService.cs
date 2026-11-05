using System.Text.Json.Nodes;
using System.Text;
using System.Text.Json;
using System.Globalization;
using System.Security.Cryptography;

namespace TouhouLastWorld;

internal sealed class MissionsService
{
    internal MissionsService(EconomyService economyService, PlayerState playerState, QuestsService questsService, SessionState sessionState)
    {
        EconomyService = economyService;
        PlayerState = playerState;
        QuestsService = questsService;
        SessionState = sessionState;
    }

    private readonly EconomyService EconomyService;
    private readonly PlayerState PlayerState;
    private readonly QuestsService QuestsService;
    private readonly SessionState SessionState;
    internal JsonObject? OpenBattlePassPeriod(LocalBattlePassPeriod? period)
    {
        if (period is null || period.Id <= 0 || period.InitialLevel < 0)
            return null;
        var samePeriod = PlayerState.user["battle_pass"]?["battle_pass_id"]?.GetValue<int>() == period.Id;
        if (!samePeriod || PlayerState.user["_local_battle_pass_top"] is not JsonObject)
        {
            var updated = PlayerState.user.DeepClone().AsObject();
            if (!samePeriod)
                updated["battle_pass"] = new JsonObject
                {
                    ["battle_pass_id"] = period.Id,
                    ["is_premium"] = 0,
                    ["is_premium_2"] = 0,
                    ["battle_pass_level"] = period.InitialLevel,
                    ["battle_pass_exp"] = 0
                };
            updated["_local_battle_pass_top"] = QueryCatalog.QueryTemplates["battle_pass_top"].DeepClone();
            PlayerState.Save(updated);
        }

        return PlayerState.user["battle_pass"]!.DeepClone().AsObject();
    }

    internal LocalReply ReceiveBattlePassRewards(LocalBattlePassRewards? request)
    {
        var updated = PlayerState.user.DeepClone().AsObject();
        if (request is null || !request.Active || request.PassId <= 0 || updated["battle_pass"] is not JsonObject status || status["battle_pass_id"]?.GetValue<int>() != request.PassId || updated["_local_battle_pass_top"] is not JsonObject top)
            return new(503, null, "Stage reward state unavailable.");
        var level = status["battle_pass_level"]?.GetValue<int>() ?? -1;
        var fields = new[]
        {
            "normal_received_reward_level",
            "premium_received_reward_level_1",
            "premium_received_reward_level_2"
        };
        var enabled = new[]
        {
            true,
            (status["is_premium"]?.GetValue<int>() ?? 0) > 0,
            (status["is_premium_2"]?.GetValue<int>() ?? 0) > 0
        };
        var result = new JsonObject
        {
            ["not_receive_message"] = ""
        };
        var rewards = new JsonArray();
        var changed = false;
        for (var track = 0; track < fields.Length; track++)
        {
            var field = fields[track];
            var received = top[field]?.GetValue<int>() ?? 0;
            if (received < 0 || level < received || level < 0)
                return new(503, null, "Invalid stage claim progress.");
            if (enabled[track])
            {
                var pending = request.Levels.Where(row => row.Level > received && row.Level <= level).OrderBy(row => row.Level).ToArray();
                if (pending.Length != level - received || pending.Where((row, index) => row.Level != received + index + 1).Any())
                    return new(503, null, "Stage reward levels unavailable.");
                foreach (var row in pending)
                {
                    var grants = track switch
                    {
                        0 => row.Normal,
                        1 => row.Premium,
                        _ => row.Premium2
                    };
                    if (grants is null)
                        return new(503, null, "Stage reward group unsupported.");
                    foreach (var grant in grants)
                    {
                        var error = EconomyService.ApplyInventoryReward(updated, grant);
                        if (error is not null)
                            return error;
                        rewards.Add(new JsonObject { ["object_id"] = grant.Id, ["object_type"] = grant.Type, ["object_value"] = grant.Count });
                    }
                }

                if (received != level)
                    changed = true;
                received = level;
                top[field] = received;
            }

            result[field] = received;
        }

        if (changed)
            PlayerState.Save(updated);
        result["received_reward_list"] = rewards;
        result["user_update"] = new JsonObject
        {
            ["gold"] = updated["gold"]!.DeepClone(),
            ["crystal"] = updated["crystal"]!.DeepClone(),
            ["item"] = updated["item"]!.DeepClone()
        };
        return new(0, result);
    }

    internal LocalReply BattlePassOverview(LocalBattlePassDaily? daily)
    {
        var body = QueryCatalog.QueryTemplates["battle_pass_top"].DeepClone().AsObject();
        if (PlayerState.user["_local_battle_pass_top"] is JsonObject saved)
            foreach (var field in body.Select(pair => pair.Key).ToArray())
                if (saved[field] is { } value)
                    body[field] = value.DeepClone();
        PopulateBattlePassDaily(body["battle_pass_mission"]!.AsObject(), daily);
        return new(0, body);
    }

    internal LocalBattlePassTask[] PopulateBattlePassDaily(JsonObject missions, LocalBattlePassDaily? daily)
    {
        if (daily is null || PlayerState.user["battle_pass"]?["battle_pass_id"]?.GetValue<int>() != daily.PassId || PlayerState.user[QuestsService.DailyQuestDay]?.GetValue<int>() != daily.Day.Date || daily.Day.StartAt(daily.SwitchingHour) is not { } start || missions["unlock_list"] is not JsonArray open || missions["clear_list"] is not JsonArray ready || missions["received_list"] is not JsonArray received)
            return [];
        var startAt = start.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        var endAt = start.AddDays(1).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        var rules = daily.Tasks.Select(task => task.Rule).ToArray();
        var ids = rules.Select(rule => rule.Id).ToHashSet();
        foreach (var list in new[]
        {
            open,
            ready,
            received
        }

        )
            foreach (var row in list.Where(row => ids.Contains(row?["id"]?.GetValue<int>() ?? 0) && (!ReferenceEquals(list, received) || row?["start_at"]?.GetValue<string>() != startAt)).ToArray())
                list.Remove(row);
        var claimed = received.Select(row => row?["id"]?.GetValue<int>() ?? 0).ToHashSet();
        foreach (var task in daily.Tasks)
        {
            var rule = task.Rule;
            var receivedToday = claimed.Contains(rule.Id);
            var progress = receivedToday ? rule.Required : Math.Min(rule.Required, task.CompleteType switch
            {
                1 => SessionState.lastLoginAt >= start && SessionState.lastLoginAt <= daily.Now ? 1 : 0,
                QuestsService.BattleWinMissionType => PlayerState.user[QuestsService.DailyBattleWins]?.GetValue<int>() ?? 0,
                DailyQuestMissionType => QuestsService.HasDailyQuestWin(daily.Day) ? 1 : 0,
                _ => 0
            });
            var row = new JsonObject
            {
                ["id"] = rule.Id,
                ["type"] = 1,
                ["name"] = task.Name,
                ["complete_type"] = task.CompleteType,
                ["complete_id"] = 0,
                ["complete_value"] = rule.Required,
                ["display_order"] = task.DisplayOrder,
                ["open_mission_id"] = 0,
                ["event_id"] = 0,
                ["chapter_id"] = 0,
                ["current_value"] = progress,
                ["start_at"] = startAt,
                ["end_at"] = endAt,
                ["battle_pass_id"] = daily.PassId,
                ["reward_battle_pass_exp"] = rule.Experience
            };
            SetMissionRewardGroups(row, task.RewardGroups);
            // 原生任务面板从开放列表创建卡片，再通过完成及已领奖列表设置状态。
            open.Add(row);
            if (progress >= rule.Required && !receivedToday)
                ready.Add(row.DeepClone());
        }

        return rules;
    }

    internal LocalReply ReceiveBattlePassMission(LocalBattlePassClaim? request, LocalBattlePassDaily? daily)
    {
        if (request is null || request.All is < 0 or > 1 || (request.All == 0 ? request.Id <= 0 : request.Id != 0))
            return new(400, null, "Invalid stage mission claim.");
        var updated = PlayerState.user.DeepClone().AsObject();
        if (updated["battle_pass"] is not JsonObject status || request.PassId <= 0 || status["battle_pass_id"]?.GetValue<int>() != request.PassId || !request.Active)
            return new(409, null, "Stage is unavailable.");
        if (updated["_local_battle_pass_top"]?["battle_pass_mission"] is not JsonObject missions || missions["clear_list"] is not JsonArray ready || missions["received_list"] is not JsonArray received || missions["unlock_list"] is not JsonArray)
            return new(503, null, "Stage mission state unavailable.");
        var exp = status["battle_pass_exp"]?.GetValue<int>() ?? -1;
        var level = status["battle_pass_level"]?.GetValue<int>() ?? -1;
        var levels = request.Levels.OrderBy(row => row.Key).ToArray();
        if (exp < 0 || level < 0 || levels.Length == 0 || !request.Levels.ContainsKey(level) || levels.Any(row => row.Key < 0 || row.Value < 0) || levels.Zip(levels.Skip(1)).Any(pair => pair.Second.Key != pair.First.Key + 1 || pair.Second.Value <= pair.First.Value) || levels.LastOrDefault(row => row.Value <= exp).Key != level || exp < request.Levels[level])
            return new(503, null, "Stage experience definition unavailable.");
        var rules = request.Tasks.Concat(PopulateBattlePassDaily(missions, daily)).ToLookup(rule => rule.Id);
        var selected = ready.Where(row => request.All == 1 || row?["id"]?.GetValue<int>() == request.Id).ToArray();
        if (selected.Length == 0 && request.All == 0)
            return new(404, null, "Stage mission is not claimable.");
        long total = exp;
        var ids = new HashSet<int>();
        var rewards = new JsonArray();
        foreach (var mission in selected)
        {
            var id = mission?["id"]?.GetValue<int>() ?? 0;
            var matches = rules[id].ToArray();
            if (id <= 0 || !ids.Add(id) || received.Any(row => row?["id"]?.GetValue<int>() == id))
                return new(409, null, "Stage mission already received or duplicated.");
            if (matches.Length != 1)
                return new(503, null, "Stage mission definition unavailable.");
            var rule = matches[0];
            if (!rule.Active || rule.Required <= 0 || rule.Experience <= 0 || mission!["battle_pass_id"]?.GetValue<int>() != request.PassId || mission["complete_value"]?.GetValue<int>() != rule.Required || mission["reward_battle_pass_exp"]?.GetValue<int>() != rule.Experience || mission["current_value"]?.GetValue<int>() is not int current || current < rule.Required)
                return new(503, null, "Stage progress or reward definition unavailable.");
            total += rule.Experience;
            if (total > int.MaxValue)
                return new(409, null, "Stage experience exceeds capacity.");
            foreach (var reward in rule.Rewards ?? [])
            {
                if (ApplyMissionReward(updated, reward) is { } error)
                    return error;
                rewards.Add(new JsonObject { ["object_id"] = reward.Id, ["object_type"] = reward.Type, ["object_value"] = reward.Count });
            }

            ready.Remove(mission);
            received.Add(mission);
        }

        status["battle_pass_exp"] = (int)total;
        status["battle_pass_level"] = levels.Last(row => row.Value <= total).Key;
        if (selected.Length > 0)
            PlayerState.Save(updated);
        var userUpdate = new JsonObject
        {
            ["battle_pass"] = status.DeepClone()
        };
        if (rewards.Count > 0)
            foreach (var field in new[]
            {
                "gold",
                "crystal",
                "item",
                "phantasm_point",
                "mop_up_day_coupon"
            }

            )
                if (updated[field] is { } value)
                    userUpdate[field] = value.DeepClone();
        return new(0, new() { ["battle_pass_mission"] = missions.DeepClone(), ["reward_list"] = rewards, ["user_update"] = userUpdate });
    }

    internal const int DailySweepRewardType = 104;
    internal const int DailyQuestMissionType = 8;
    internal const int NoviceMissionType = 10;
    internal const int UnitLevelMissionType = 22;
    internal const int UnitLimitBreakMissionType = 23;
    internal const int UnitAwakeningMissionType = 25;
    internal const string DailyQuestMissionClaim = "_local_daily_quest_mission_claimed_on";
    internal const string UnitLimitBreakMilestone = "_local_unit_limit_break_completed";
    internal const string UnitAwakeningMilestone = "_local_unit_awakening_completed";
    internal LocalReply MissionOverview(LocalDailyLoginMission? daily, LocalProgressMission[]? progress, LocalDailyQuestMission? dailyQuest, LocalBattlePassPeriod? period)
    {
        var body = QueryCatalog.QueryTemplates["mission_top"].DeepClone().AsObject();
        if (OpenBattlePassPeriod(period) is { } pass)
            body["user_update"] = new JsonObject
            {
                ["battle_pass"] = pass
            };
        var overview = BuildMissionLists(daily, progress, dailyQuest);
        body["mission"] = overview;
        var rows = overview["unlock_list"]!.AsArray().Concat(overview["clear_list"]!.AsArray()).ToArray();
        foreach (var field in new[]
        {
            "event_id",
            "chapter_id"
        }

        )
            body[field + "_list"] = new JsonArray(rows.Select(row => row?[field]?.GetValue<int>() ?? 0).Where(id => id > 0).Distinct().Select(id => (JsonNode?)JsonValue.Create(id)).ToArray());
        return new(0, body);
    }

    internal JsonObject BuildMissionLists(LocalDailyLoginMission? daily, LocalProgressMission[]? progress, LocalDailyQuestMission? dailyQuest)
    {
        var saved = PlayerState.user["_local_missions"] as JsonObject;
        var overview = (saved ?? QueryCatalog.QueryTemplates["mission_top"]["mission"]!.AsObject()).DeepClone().AsObject();
        var login = DailyLoginRow(daily, overview);
        if (login is not null)
            overview[login["current_value"]!.GetValue<int>() == 1 ? "clear_list" : "unlock_list"]!.AsArray().Add(login);
        var quest = DailyQuestMissionRow(dailyQuest, overview);
        if (quest is not null)
            overview[quest["current_value"]!.GetValue<int>() == 1 ? "clear_list" : "unlock_list"]!.AsArray().Add(quest);
        foreach (var row in ProgressMissionRows(progress, overview))
            overview[MissionComplete(row) ? "clear_list" : "unlock_list"]!.AsArray().Add(row);
        return overview;
    }

    internal static bool MissionComplete(JsonObject row) => row["current_value"]!.GetValue<int>() >= row["complete_value"]!.GetValue<int>();
    internal IEnumerable<JsonObject> ProgressMissionRows(LocalProgressMission[]? progress, JsonObject missions)
    {
        if (progress is null)
            yield break;
        var displayed = missions["unlock_list"]!.AsArray().Concat(missions["clear_list"]!.AsArray()).Select(row => row?["id"]?.GetValue<int>() ?? 0).ToHashSet();
        var received = (PlayerState.user["_local_received_missions"] as JsonArray)?.Select(id => id!.GetValue<int>()).ToHashSet() ?? [];
        var episodes = (PlayerState.user["quest_playdata"] as JsonArray)?.ToLookup(row => row?["episode_id"]?.GetValue<int>() ?? 0);
        var highestLevel = PlayerProgress.HighestUnitLevel(PlayerState.user);
        var units = PlayerState.user["unit"]?["unit_list"] as JsonArray;
        var limitBroken = PlayerState.user[UnitLimitBreakMilestone]?.GetValue<bool>() == true || units?.Any(row => row?["level_limitbreak"]?.GetValue<int>() is > 0) == true;
        var awakened = PlayerState.user[UnitAwakeningMilestone]?.GetValue<bool>() == true || units?.Any(row => row?["rare"]?.GetValue<int>() is > 1) == true;
        foreach (var mission in progress)
        {
            if (received.Contains(mission.Id) || !displayed.Add(mission.Id))
                continue;
            var battle = mission.BattleId > 0;
            var completed = episodes?[mission.EpisodeId].Any(episode => battle ? episode?["normal"]?["status"]?.GetValue<int>() is >= 2 : episode?["scenario_status"]?.GetValue<int>() == 1) == true;
            var current = mission.UnitProgressType switch
            {
                UnitLevelMissionType => Math.Min(highestLevel, mission.Required),
                UnitLimitBreakMissionType => limitBroken ? 1 : 0,
                UnitAwakeningMissionType => awakened ? 1 : 0,
                _ => completed ? 1 : 0
            };
            var row = new JsonObject
            {
                ["id"] = mission.Id,
                ["type"] = mission.Type,
                ["name"] = mission.Name,
                ["complete_type"] = mission.UnitProgressType > 0 ? mission.UnitProgressType : battle ? 4 : 3,
                ["complete_id"] = battle ? mission.BattleId : mission.EpisodeId,
                ["complete_value"] = mission.Required,
                ["display_order"] = mission.DisplayOrder,
                ["open_mission_id"] = 0,
                ["event_id"] = 0,
                ["chapter_id"] = 0,
                ["current_value"] = current,
                ["start_at"] = "",
                ["end_at"] = ""
            };
            SetMissionRewardGroups(row, mission.RewardGroups);
            yield return row;
        }
    }


    internal JsonArray NoviceMissionBadge(LocalProgressMission[]? progress) => new(BuildMissionLists(null, progress, null)["clear_list"]!.AsArray().Where(row => row?["type"]?.GetValue<int>() == NoviceMissionType).OrderBy(row => row!["display_order"]?.GetValue<int>() ?? 0).Select(row => row!["id"]!.DeepClone()).ToArray());
    internal JsonObject? DailyLoginRow(LocalDailyLoginMission? daily, JsonObject missions)
    {
        if (daily is null || daily.Id <= 0 || daily.SwitchingHour is < 0 or > 23 || daily.RewardGroups.Length != 4)
            return null;
        var day = daily.DayStart;
        if (DateOnly.TryParseExact(PlayerState.user["_local_daily_login_mission_claimed_on"]?.GetValue<string>(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var claimed) && claimed >= DateOnly.FromDateTime(day))
            return null;
        if (missions["unlock_list"]!.AsArray().Concat(missions["clear_list"]!.AsArray()).Any(row => row?["id"]?.GetValue<int>() == daily.Id) || PlayerState.user["_local_received_missions"]?.AsArray().Any(id => id?.GetValue<int>() == daily.Id) == true)
            return null;
        var login = new JsonObject
        {
            ["id"] = daily.Id,
            ["type"] = 3,
            ["name"] = daily.Name,
            ["complete_type"] = 1,
            ["complete_id"] = 0,
            ["complete_value"] = 1,
            ["display_order"] = daily.DisplayOrder,
            ["open_mission_id"] = 0,
            ["event_id"] = 0,
            ["chapter_id"] = 0,
            ["current_value"] = SessionState.lastLoginAt >= day && SessionState.lastLoginAt <= daily.Now ? 1 : 0,
            ["start_at"] = day.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            ["end_at"] = day.AddDays(1).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
        };
        SetMissionRewardGroups(login, daily.RewardGroups);
        return login;
    }

    internal static void SetMissionRewardGroups(JsonObject row, (int Id, int Count)[] groups)
    {
        for (var index = 0; index < groups.Length; index++)
        {
            var reward = groups[index];
            row[$"reward_group{index + 1}_id"] = reward.Id;
            row[$"reward_group{index + 1}_value"] = reward.Count;
        }
    }

    internal JsonObject? DailyQuestMissionRow(LocalDailyQuestMission? definition, JsonObject missions)
    {
        if (definition is null || definition.Id <= 0 || definition.Day.StartAt(definition.SwitchingHour) is not { } start || definition.RewardGroups.Length != 4 || definition.Day.Date != PlayerState.user[QuestsService.DailyQuestDay]?.GetValue<int>() || definition.Day.Date <= (PlayerState.user[DailyQuestMissionClaim]?.GetValue<int>() ?? 0) || missions["unlock_list"]!.AsArray().Concat(missions["clear_list"]!.AsArray()).Any(row => row?["id"]?.GetValue<int>() == definition.Id))
            return null;
        var won = QuestsService.HasDailyQuestWin(definition.Day);
        var row = new JsonObject
        {
            ["id"] = definition.Id,
            ["type"] = 3,
            ["name"] = definition.Name,
            ["complete_type"] = DailyQuestMissionType,
            ["complete_id"] = 0,
            ["complete_value"] = 1,
            ["display_order"] = definition.DisplayOrder,
            ["open_mission_id"] = 0,
            ["event_id"] = 0,
            ["chapter_id"] = 0,
            ["current_value"] = won ? 1 : 0,
            ["start_at"] = start.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            ["end_at"] = start.AddDays(1).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
        };
        SetMissionRewardGroups(row, definition.RewardGroups);
        return row;
    }

    internal LocalReply ReceiveMission(LocalMissionClaim? request, LocalDailyLoginMission? daily, LocalProgressMission[]? progress, LocalDailyQuestMission? dailyQuest)
    {
        if (request is null || request.All is < 0 or > 1 || request.Type < 0 || request.EventId < 0 || request.ChapterId < 0 || (request.All == 0 ? request.Id <= 0 : request.Id != 0))
            return new(400, null, "Invalid mission claim.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var missions = updated["_local_missions"] as JsonObject;
        if (missions is null && daily is null && progress is null && dailyQuest is null)
            return new(404, null, "Mission state unavailable.");
        missions ??= QueryCatalog.QueryTemplates["mission_top"]["mission"]!.DeepClone().AsObject();
        var ready = missions["clear_list"] as JsonArray;
        if (ready is null || missions["unlock_list"] is not JsonArray)
            return new(404, null, "Mission state unavailable.");
        var login = DailyLoginRow(daily, missions);
        var quest = DailyQuestMissionRow(dailyQuest, missions);
        var claimable = ready.Concat(ProgressMissionRows(progress, missions).Where(MissionComplete));
        if (login?["current_value"]?.GetValue<int>() == 1)
            claimable = claimable.Append(login);
        if (quest?["current_value"]?.GetValue<int>() == 1)
            claimable = claimable.Append(quest);
        var selected = claimable.Where(row => row is JsonObject && (request.All == 1 || row["id"]?.GetValue<int>() == request.Id) && (request.Type == 0 || row["type"]?.GetValue<int>() == request.Type) && (request.EventId == 0 || row["event_id"]?.GetValue<int>() == request.EventId) && (request.ChapterId == 0 || row["chapter_id"]?.GetValue<int>() == request.ChapterId)).ToArray();
        if (selected.Length == 0 && request.All == 0)
            return new(404, null, "Mission is not claimable.");
        var received = updated["_local_received_missions"] as JsonArray ?? new JsonArray();
        if (updated["_local_received_missions"] is null)
            updated["_local_received_missions"] = received;
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
            if (rule is null || !rule.Active || rule.Required <= 0 || rule.Rewards.Length == 0 || mission["type"]?.GetValue<int>() != rule.Type || mission["event_id"]?.GetValue<int>() != rule.EventId || mission["complete_value"]?.GetValue<int>() != rule.Required || mission["current_value"]?.GetValue<int>() is not int current || current < rule.Required)
                return new(503, null, "Mission progress or reward definition unavailable.");
            var objects = new JsonArray();
            foreach (var reward in rule.Rewards)
            {
                var error = reward.Type == 9 ? ApplyMissionUnitReward(updated, reward) : ApplyMissionReward(updated, reward);
                if (error is not null)
                    return error;
                var item = new JsonObject
                {
                    ["object_type"] = reward.Type,
                    ["object_id"] = reward.Id,
                    ["object_value"] = reward.Count,
                    ["custom_parameter"] = ""
                };
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

        if (selected.Length > 0)
            PlayerState.Save(updated);
        var userUpdate = new JsonObject
        {
            ["gold"] = updated["gold"]!.DeepClone(),
            ["crystal"] = updated["crystal"]!.DeepClone(),
            ["item"] = updated["item"]!.DeepClone()
        };
        if (rewards.Any(reward => reward?["object_type"]?.GetValue<int>() == 2))
            userUpdate["phantasm_point"] = updated["phantasm_point"]!.DeepClone();
        if (rewards.Any(reward => reward?["object_type"]?.GetValue<int>() == 5))
            userUpdate["piece"] = updated["piece"]!.DeepClone();
        if (rewards.Any(reward => reward?["object_type"]?.GetValue<int>() == 9))
        {
            userUpdate["unit"] = updated["unit"]!.DeepClone();
            userUpdate["album"] = updated["album"]!.DeepClone();
        }

        if (updated["mop_up_day_coupon"] is JsonNode coupons)
            userUpdate["mop_up_day_coupon"] = coupons.DeepClone();
        return new(0, new() { ["mission"] = BuildMissionLists(daily, progress, dailyQuest), ["reward_list"] = rewards, ["receive_mission_list"] = receipts, ["not_receive_message"] = "", ["user_update"] = userUpdate });
    }

    internal static LocalReply? ApplyMissionReward(JsonObject updated, LocalInventoryReward reward) => reward.Type switch
    {
        2 => ApplyMissionSpiritReward(updated, reward),
        5 => ApplyMissionPieceReward(updated, reward),
        DailySweepRewardType => ApplyDailySweepReward(updated, reward),
        _ => EconomyService.ApplyInventoryReward(updated, reward)
    };
    internal static LocalReply? ApplyMissionPieceReward(JsonObject updated, LocalInventoryReward reward)
    {
        if (reward.Id != 0 || reward.Count <= 0 || reward.Capacity <= 0)
            return new(503, null, "Mission piece definition unsupported.");
        var total = (long)updated["piece"]!.GetValue<int>() + reward.Count;
        if (total > reward.Capacity)
            return new(409, null, "Mission pieces exceed capacity.");
        updated["piece"] = (int)total;
        return null;
    }

    internal LocalReply? ApplyMissionUnitReward(JsonObject updated, LocalInventoryReward reward)
    {
        if (reward.Id <= 0 || reward.CostumeId <= 0 || reward.Count != 1 || reward.Capacity <= 0)
            return new(503, null, "Mission partner definition unsupported.");
        var sequence = EconomyService.GetLastUnitId(updated);
        if (updated["unit"]!["unit_list"]!.AsArray().Count >= reward.Capacity || sequence is < 0 or long.MaxValue)
            return new(409, null, "Mission partner exceeds inventory capacity.");
        EconomyService.AddOwnedUnit(updated, reward.Id, reward.CostumeId, sequence + 1);
        return null;
    }

    internal static LocalReply? ApplyMissionSpiritReward(JsonObject updated, LocalInventoryReward reward)
    {
        if (reward.Id != 0 || reward.Count <= 0 || reward.Capacity <= 0)
            return new(503, null, "Mission spirit point definition unsupported.");
        var points = updated["phantasm_point"]!;
        var total = (long)points["points"]!.GetValue<int>() + reward.Count;
        if (total > reward.Capacity)
            return new(409, null, "Mission spirit point reward exceeds capacity.");
        points["points"] = (int)total;
        return null;
    }

    internal static LocalReply? ApplyDailySweepReward(JsonObject updated, LocalInventoryReward reward)
    {
        if (reward.Id != 0 || reward.Count <= 0)
            return new(503, null, "Daily sweep coupon definition unsupported.");
        var total = (long)(updated["mop_up_day_coupon"]?.GetValue<int>() ?? 0) + reward.Count;
        if (total > reward.Capacity)
            return new(409, null, "Daily sweep coupon reward exceeds capacity.");
        updated["mop_up_day_coupon"] = (int)total;
        return null;
    }
}
