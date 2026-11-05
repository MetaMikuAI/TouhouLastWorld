using System.Text.Json.Nodes;
using System.Text;
using System.Text.Json;
using System.Globalization;
using System.Security.Cryptography;

namespace TouhouLastWorld;

internal sealed class QuestsService
{
    internal QuestsService(PlayerState playerState, SessionState sessionState, TeamsService teamsService)
    {
        PlayerState = playerState;
        SessionState = sessionState;
        TeamsService = teamsService;
    }

    private readonly PlayerState PlayerState;
    private readonly SessionState SessionState;
    private readonly TeamsService TeamsService;
    internal LocalReply CompleteEpisode(CompleteEpisodeCommand? request)
    {
        if (request?.EntityId is null or <= 0 or > int.MaxValue)
            return new(400, null, "Positive episode ID required.");
        if (request.Value is null)
            return new(503, null, "Episode master data unavailable.");
        if (request.Value != 1)
            return new(404, null, "Story episode not found.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var episodes = updated["quest_playdata"] as JsonArray ?? new JsonArray();
        var episode = episodes.FirstOrDefault(entry => entry?["episode_id"]?.GetValue<int>() == request.EntityId) as JsonObject;
        if (episode is null)
        {
            episode = new JsonObject
            {
                ["episode_id"] = (int)request.EntityId.Value
            };
            foreach (var difficulty in new[]
            {
                "normal",
                "hard",
                "lunatic"
            }

            )
                episode[difficulty] = new JsonObject
                {
                    ["status"] = -2,
                    ["battle_challenge"] = new JsonArray()
                };
            episodes.Add(episode);
        }

        episode["scenario_status"] = Math.Max(1, episode["scenario_status"]?.GetValue<int>() ?? 0);
        if (episodes.Parent is null)
            updated["quest_playdata"] = episodes;
        PlayerState.Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["quest_playdata"] = episodes.DeepClone() } });
    }

    internal LocalReply AutoReplayUnavailable(AutoReplayUnavailableCommand? request)
    {
        var target = request?.ReplayMeasure;
        if (target is null || target.QuestType is < 0 or > 4 || target.TargetId <= 0 || target.Difficulty < 0 || request!.Value is null or <= 0)
            return new(400, null, "Invalid background replay request.");
        var replay = PlayerState.user["auto_replay"];
        if (replay?["target_id"]?.GetValue<long>() != 0)
            return new(409, null, "Existing background replay must be settled first.");
        return new(0, new() { ["user_update"] = new JsonObject { ["auto_replay"] = replay.DeepClone() } });
    }

    internal const string DailyQuestDay = "_local_daily_quest_day";
    internal const string DailyQuestList = "daily_quest_count_list";
    internal const string DailyBattleWins = "_local_daily_battle_wins";
    internal const int BattleWinMissionType = 7;
    internal int PendingDailyQuestStage => PlayerState.user[PendingQuestBattle]?["daily_stage_id"]?.GetValue<int>() ?? 0;

    internal bool HasDailyQuestWin(LocalDailyQuestDay day) => PlayerState.user[DailyQuestDay]?.GetValue<int>() == day.Date && (PlayerState.user[DailyQuestList] as JsonArray)?.Any(quest => quest?["count"]?.GetValue<int>() is > 0 && day.QuestIds.Contains(quest["daily_quest_id"]!.GetValue<int>())) == true;
    internal bool RefreshDailyQuestDay(LocalDailyQuestDay? day)
    {
        if (day is null || day.Date <= (PlayerState.user[DailyQuestDay]?.GetValue<int>() ?? 0))
            return false;
        var updated = PlayerState.user.DeepClone().AsObject();
        updated[DailyQuestDay] = day.Date;
        updated.Remove(DailyBattleWins);
        var changed = false;
        if (updated[DailyQuestList] is JsonArray quests && PlayerState.user[DailyQuestDay] is not null)
        {
            var dailyIds = day.QuestIds.ToHashSet();
            foreach (var quest in quests)
            {
                if (!dailyIds.Contains(quest!["daily_quest_id"]!.GetValue<int>()) || quest["count"]?.GetValue<int>() is not > 0)
                    continue;
                quest["count"] = 0;
                changed = true;
            }
        }

        PlayerState.Save(updated);
        return changed;
    }

    internal LocalReply? CheckDailyQuest(LocalDailyQuestEntry entry)
    {
        if (entry.QuestId <= 0 || entry.StageId <= 0 || entry.AttemptLimit <= 0 || entry.RequiredLevel < 0 || PlayerState.user[DailyQuestDay] is null)
            return new(503, null, "Daily quest rules unavailable.");
        if (PlayerState.user["level"]!.GetValue<int>() < entry.RequiredLevel)
            return new(409, null, "Required account level not reached.");
        var used = (PlayerState.user[DailyQuestList] as JsonArray)?.FirstOrDefault(row => row?["daily_quest_id"]?.GetValue<int>() == entry.QuestId)?["count"]?.GetValue<int>() ?? 0;
        return used >= entry.AttemptLimit ? new(409, null, "Daily quest attempt limit reached.") : null;
    }

    internal static void UpdateDailyQuestProgress(JsonObject updated, JsonObject pending, int state, JsonArray challenges, bool won, JsonObject changes)
    {
        var questId = pending["daily_quest_id"]!.GetValue<int>();
        var stageId = pending["daily_stage_id"]!.GetValue<int>();
        var quests = updated[DailyQuestList] as JsonArray ?? new JsonArray();
        var quest = quests.FirstOrDefault(row => row?["daily_quest_id"]?.GetValue<int>() == questId) as JsonObject;
        if (quest is null)
        {
            quest = new JsonObject
            {
                ["daily_quest_id"] = questId,
                ["count"] = 0,
                ["cleared_info"] = new JsonArray()
            };
            quests.Add(quest);
        }

        var stages = quest["cleared_info"] as JsonArray ?? new JsonArray();
        var stage = stages.FirstOrDefault(row => row?["daily_quest_stage_id"]?.GetValue<int>() == stageId) as JsonObject;
        if (stage is null)
        {
            stage = new JsonObject
            {
                ["daily_quest_stage_id"] = stageId
            };
            stages.Add(stage);
        }

        stage["normal"] ??= new JsonObject();
        SetQuestProgress(stage["normal"]!, state, challenges);
        if (stages.Parent is null)
            quest["cleared_info"] = stages;
        if (won && pending[DailyQuestDay]?.GetValue<int>() == updated[DailyQuestDay]?.GetValue<int>())
            quest["count"] = (quest["count"]?.GetValue<int>() ?? 0) + 1;
        if (quests.Parent is null)
            updated[DailyQuestList] = quests;
        changes[DailyQuestList] = quests.DeepClone();
    }

    internal const string QuestWinCount = "_local_quest_win_count";
    internal const string DispatchCompletionCount = "_local_dispatch_completion_count";
    internal const string QuestClearRecords = "_local_quest_clear_records";
    internal LocalReply ClearHistoryOverview(LocalHistoryQuery? query)
    {
        if (query is null || query.Kind is not (1 or 2) || query.FirstId <= 0 || query.SecondId < 0)
            return new(400, null, "Invalid clear history query.");
        var best = FindQuestClear(query)?.DeepClone().AsObject();
        if (best is not null)
        {
            best.Remove("units");
            var roster = PlayerState.user["unit"]!["unit_list"]!.AsArray();
            var favorite = (PlayerState.user["home_unit_info"]?["unit_param"] as JsonArray)?.FirstOrDefault()?["user_unit_id"]?.GetValue<long>();
            var avatar = roster.FirstOrDefault(unit => unit?["user_unit_id"]?.GetValue<long>() == favorite) ?? best["team_units"]!.AsArray()[0]!;
            best["player_id"] = SessionState.account!.PublicId;
            best["player_name"] = PlayerState.user["player_name"]?.DeepClone();
            best["selected_achievement_id"] = PlayerState.user["achievement"]?["selected_achievement_id"]?.DeepClone();
            best["unit_id"] = avatar["unit_id"]!.DeepClone();
            best["equip_costume_id"] = avatar["equip_costume_id"]!.DeepClone();
        }

        return new(0, new() { ["best"] = best, ["ranking"] = new JsonArray(), ["justnow"] = new JsonArray() });
    }

    internal JsonNode? FindQuestClear(LocalHistoryQuery query) => query.Kind == 1 && query.SecondId == 0 ? PlayerState.user[QuestClearRecords]?[query.FirstId.ToString(CultureInfo.InvariantCulture)] : null;
    internal static void RecordQuestClear(JsonObject updated, int battleId, long[] members, LocalClearScore score)
    {
        if (score.Actions <= 0 || score.Damage <= 0 || members.Length is < 1 or > 6 || score.TeamPowers.Length != members.Length)
            return;
        var records = updated[QuestClearRecords] as JsonObject ?? new JsonObject();
        var key = battleId.ToString(CultureInfo.InvariantCulture);
        if (records[key] is { } previous)
        {
            var actions = previous["action"]!.GetValue<int>();
            if (score.Actions > actions || score.Actions == actions && score.Damage <= previous["damage"]!.GetValue<long>())
                return;
        }

        var owned = updated["unit"]!["unit_list"]!.AsArray();
        var pictures = updated["picture"]!["picture_list"]!.AsArray();
        var team = new JsonArray();
        var details = new JsonArray();
        for (var index = 0; index < members.Length; index++)
        {
            var member = owned.FirstOrDefault(row => row?["user_unit_id"]?.GetValue<long>() == members[index]);
            if (member is null)
                return;
            var detail = new JsonObject
            {
                ["unit_info"] = member.DeepClone()
            };
            for (var slot = 1; slot <= 5; slot++)
            {
                var pictureId = member[$"equip_spellcard{slot}_user_picture_id"]?.GetValue<long>() ?? 0;
                if (pictureId <= 0)
                    continue;
                var picture = pictures.FirstOrDefault(row => row?["user_picture_id"]?.GetValue<long>() == pictureId);
                if (picture is null)
                    return;
                detail[$"spellcard_picture_info_{slot}"] = picture.DeepClone();
            }

            var summary = new JsonObject
            {
                ["total_power"] = score.TeamPowers[index]
            };
            foreach (var field in new[]
            {
                "unit_id",
                "reincarnation_stage",
                "level",
                "level_limitbreak",
                "rare",
                "equip_costume_id",
                "destiny_value"
            }

            )
                summary[field] = member[field]?.DeepClone();
            team.Add(summary);
            details.Add(detail);
        }

        records[key] = new JsonObject
        {
            ["action"] = score.Actions,
            ["damage"] = score.Damage,
            ["team_units"] = team,
            ["units"] = details
        };
        if (records.Parent is null)
            updated[QuestClearRecords] = records;
    }

    internal LocalReply ProfileHistory()
    {
        var collection = PlayerState.user["album"]?["unit_list"] as JsonArray ?? new JsonArray();
        var owned = PlayerState.user["unit"]!["unit_list"]!.AsArray();
        var acquired = collection.Concat(owned).Select(row => row?["unit_id"]?.GetValue<int>() ?? 0).Where(id => id > 0).Distinct().Count();
        return new(0, new() { ["unit_get_count"] = acquired, ["quest_complete_count"] = QuestWins(), ["dispatch_complete_count"] = PlayerState.user[DispatchCompletionCount]?.GetValue<int>() ?? 0 });
    }

    internal int QuestWins()
    {
        if (PlayerState.user[QuestWinCount]?.GetValue<int>() is { } count)
            return count;
        // 旧存档只保留各难度的最佳通关状态。
        return (PlayerState.user["quest_playdata"] as JsonArray)?.Sum(episode => QuestDifficulties.Count(key => episode?[key]?["status"]?.GetValue<int>() >= 2)) ?? 0;
    }

    internal LocalReply RankingHistoryTeam(RankingHistoryTeamCommand? request)
    {
        if (request?.Value is null or <= 0 || string.IsNullOrWhiteSpace(request.Text) || request.Text.Any(char.IsControl))
            return new(400, null, "Complete ranking query required.");
        var matches = (PlayerState.user["_local_ranking_teams"] as JsonArray)?.Where(row => row?["manage_id"]?.GetValue<int>() == request.Value && row["public_id"]?.GetValue<string>() == request.Text).ToArray();
        if (matches is null || matches.Length == 0)
            return new(404, null, "Local ranking team not found.");
        if (matches.Length != 1 || matches[0] is not JsonObject source || request.HistoryDefinitions is not { } rules || source["difficulty"]?.GetValue<int>() is not (>= 0 and <= 2) || source["stage_group_master_id"]?.GetValue<int>() is not int group || rules.StageGroups?.Contains(group) != true || source["player_name"] is not JsonValue nameValue || !nameValue.TryGetValue<string>(out var name) || name.Any(char.IsControl))
            return new(503, null, "Ranking team definitions unavailable.");
        var response = new JsonObject
        {
            ["player_name"] = name,
            ["difficulty"] = source["difficulty"]!.DeepClone(),
            ["stage_group_master_id"] = group
        };
        var ended = false;
        for (var area = 1; area <= 5; area++)
        {
            var key = $"area{area}_team_info";
            if (source[key] is not JsonArray team || team.Count > 6 || area == 1 && team.Count == 0 || ended && team.Count > 0)
                return new(503, null, "Ranking areas are incomplete.");
            if (team.Count == 0)
                ended = true;
            var members = new JsonArray();
            foreach (var member in team)
            {
                if (member?["unit_id"]?.GetValue<int>() is not int unit || unit <= 0 || !rules.Units.Contains(unit) || member["costume_id"]?.GetValue<int>() is not int costume || !rules.Costumes.TryGetValue(costume, out var owner) || owner != unit)
                    return new(503, null, "Ranking member definition unavailable.");
                members.Add(new JsonObject { ["unit_id"] = unit, ["costume_id"] = costume });
            }

            response[key] = members;
        }

        return new(0, response);
    }

    internal LocalReply HistoryPlayer(HistoryPlayerCommand? request, bool summaryOnly = false)
    {
        if (string.IsNullOrWhiteSpace(request?.Text) || request.Text.Any(char.IsControl))
            return new(400, null, "Player identifier required.");
        var matches = (PlayerState.user["_local_history_players"] as JsonArray)?.Where(row => row?["player_id"]?.GetValue<string>() == request.Text).ToArray();
        if (matches is null || matches.Length == 0)
            return new(404, null, "Local player history not found.");
        if (matches.Length != 1 || matches[0] is not JsonObject source || request.HistoryDefinitions is not { } rules)
            return new(503, null, "Player history definitions unavailable.");
        var response = new JsonObject();
        foreach (var field in new[]
        {
            "unit_id",
            "costume_id",
            "home_motion",
            "unit_get_count",
            "quest_complete_count",
            "dispatch_complete_count",
            "selected_achievement_id",
            "bronze_trophy_count",
            "silver_trophy_count",
            "gold_trophy_count"
        }

        )
        {
            if (source[field] is not JsonValue value || !value.TryGetValue<int>(out var number) || number < 0)
                return new(503, null, "Player history is incomplete.");
            response[field] = number;
        }

        var unitId = response["unit_id"]!.GetValue<int>();
        var costumeId = response["costume_id"]!.GetValue<int>();
        var achievement = response["selected_achievement_id"]!.GetValue<int>();
        if (unitId <= 0 || costumeId <= 0 || !rules.Units.Contains(unitId) || !rules.Costumes.TryGetValue(costumeId, out var costumeUnit) || costumeUnit != unitId || achievement != 0 && rules.Achievements?.Contains(achievement) != true)
            return new(503, null, "Player history references unavailable definitions.");
        if (!summaryOnly)
        {
            foreach (var field in new[]
            {
                "comment",
                "last_login_at",
                "player_name"
            }

            )
            {
                if (source[field] is not JsonValue value || !value.TryGetValue<string>(out var text) || text.Any(char.IsControl))
                    return new(503, null, "Player profile is incomplete.");
                response[field] = text;
            }

            if (source["player_level"]?.GetValue<int>() is not > 0 || source["friend_status"]?.GetValue<int>() is not >= 0 || !DateTimeOffset.TryParse(source["last_login_at"]!.GetValue<string>(), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out _))
                return new(503, null, "Player profile values are invalid.");
            response["player_level"] = source["player_level"]!.DeepClone();
            response["friend_status"] = source["friend_status"]!.DeepClone();
        }

        return new(0, response);
    }

    internal LocalReply HistoryTeam(HistoryTeamCommand? request)
    {
        var query = request?.HistoryQuery;
        if (query is null || query.Kind < 0 || query.FirstId < 0 || query.SecondId < 0 || query.Type < 0 || string.IsNullOrWhiteSpace(query.PlayerId) || query.PlayerId.Any(char.IsControl))
            return new(400, null, "Complete history query required.");
        var personal = query.Type == 0 && query.PlayerId == SessionState.account!.PublicId ? FindQuestClear(query) : null;
        var matches = personal is not null ? new[]
        {
            personal
        }

        : (PlayerState.user["_local_history_teams"] as JsonArray)?.Where(row => row?["kind"]?.GetValue<int>() == query.Kind && row["uniq_id1"]?.GetValue<int>() == query.FirstId && row["uniq_id2"]?.GetValue<int>() == query.SecondId && row["type"]?.GetValue<int>() == query.Type && row["player_id"]?.GetValue<string>() == query.PlayerId).ToArray();
        if (matches is null || matches.Length == 0)
            return new(404, null, "Local history not found.");
        var rules = request!.HistoryDefinitions;
        if (matches.Length != 1 || matches[0]?["units"] is not JsonArray { Count: > 0 and <= 6 } units || rules is null)
            return new(503, null, "History snapshot unavailable.");
        foreach (var entry in units)
        {
            var unit = entry?["unit_info"];
            if (unit is not JsonObject || unit["unit_id"]?.GetValue<int>() is not > 0 || !rules.Units.Contains(unit["unit_id"]!.GetValue<int>()) || unit["equip_costume_id"]?.GetValue<int>() is not > 0 || !rules.Costumes.TryGetValue(unit["equip_costume_id"]!.GetValue<int>(), out var costumeUnit) || costumeUnit != unit["unit_id"]!.GetValue<int>() || unit["level"]?.GetValue<int>() is not > 0 || unit["rare"]?.GetValue<int>() is not > 0 || unit["strong_fragrant_wood"] is not JsonObject || unit["strong_fragrant_wood_limit_info"] is not JsonObject)
                return new(503, null, "History unit definition unavailable.");
            for (var slot = 1; slot <= 5; slot++)
            {
                var picture = entry![$"spellcard_picture_info_{slot}"];
                if (picture is not null && (picture is not JsonObject || picture["picture_id"]?.GetValue<int>() is not > 0 || !rules.Pictures.Contains(picture["picture_id"]!.GetValue<int>())))
                    return new(503, null, "History equipment definition unavailable.");
            }
        }

        return new(0, new() { ["units"] = units.DeepClone() });
    }

    internal const string PendingQuestBattle = "_local_quest_battle_pending";
    internal const string ChargeQuestOnWin = "charge_on_win";
    internal static string[] QuestDifficulties = ["normal", "hard", "lunatic"];
    internal int PendingQuestBattleEpisode => PlayerState.user[PendingQuestBattle]?["episode_id"]?.GetValue<int>() ?? 0;
    internal int PendingQuestBattleDifficulty => PlayerState.user[PendingQuestBattle]?["difficulty"]?.GetValue<int>() ?? -1;
    internal long[] PendingQuestBattleOwnedUnits => (PlayerState.user[PendingQuestBattle]?["owned"] as JsonArray)?.Select(id => id!.GetValue<long>()).ToArray() ?? [];

    internal LocalReply StartQuestBattle(LocalQuestBattleStart? request)
    {
        if (request is null)
            return new(501, null, "This battle entry is unavailable offline.");
        var daily = request.Daily;
        if ((daily is null ? request.EpisodeId <= 0 : request.EpisodeId != 0 || request.Difficulty != 0 || request.TeamNumber == 0) || request.BattleId <= 0 || request.Difficulty is < 0 or > 2 || request.TeamNumber < 0 || request.Cost < 0 || request.Participants.Length == 0 || request.Participants.Any(id => id <= 0) || request.OwnedParticipants.Any(id => !request.Participants.Contains(id)))
            return new(400, null, "Invalid quest battle entry.");
        if (!request.Open)
            return new(409, null, "Quest battle is locked.");
        if (daily is not null && CheckDailyQuest(daily) is { } unavailable)
            return unavailable;
        if (request.TeamNumber == 0)
        {
            if (!request.OwnedParticipants.SequenceEqual(TeamsService.storyTeamMembers))
                return new(409, null, "Battle formation does not match the story team.");
        }
        else
        {
            var team = PlayerState.user["team"]!["team_list"]!.AsArray().FirstOrDefault(row => row?["number"]?.GetValue<int>() == request.TeamNumber);
            if (team?["user_unit_id_list"] is not JsonArray slots || request.OwnedParticipants.Any(id => !slots.Any(slot => slot!.GetValue<long>() == id)))
                return new(409, null, "Battle formation does not match the saved team.");
        }

        var points = PlayerState.user["phantasm_point"]!["points"]!.GetValue<int>();
        if (points < request.Cost)
            return new(409, null, "Not enough spirit points.");
        var pending = PlayerState.user[PendingQuestBattle];
        if (pending?["episode_id"]?.GetValue<int>() == request.EpisodeId && pending["battle_id"]?.GetValue<int>() == request.BattleId && (daily is null || pending["daily_stage_id"]?.GetValue<int>() == daily.StageId && pending[DailyQuestDay]?.GetValue<int>() == PlayerState.user[DailyQuestDay]?.GetValue<int>()) && pending["difficulty"]?.GetValue<int>() == request.Difficulty && pending["team_number"]?.GetValue<int>() == request.TeamNumber && pending["cost"]?.GetValue<int>() == request.Cost && pending["participants"]!.AsArray().Select(id => id!.GetValue<long>()).SequenceEqual(request.Participants))
            return QuestBattleAdmissionReply();
        var updated = PlayerState.user.DeepClone().AsObject();
        updated[PendingQuestBattle] = new JsonObject
        {
            ["episode_id"] = request.EpisodeId,
            ["battle_id"] = request.BattleId,
            ["difficulty"] = request.Difficulty,
            ["team_number"] = request.TeamNumber,
            ["cost"] = request.Cost,
            [ChargeQuestOnWin] = true,
            ["participants"] = new JsonArray(request.Participants.Select(id => (JsonNode?)JsonValue.Create(id)).ToArray()),
            ["owned"] = new JsonArray(request.OwnedParticipants.Select(id => (JsonNode?)JsonValue.Create(id)).ToArray())
        };
        if (daily is not null)
        {
            var entry = updated[PendingQuestBattle]!;
            entry["daily_stage_id"] = daily.StageId;
            entry["daily_quest_id"] = daily.QuestId;
            entry[DailyQuestDay] = updated[DailyQuestDay]!.DeepClone();
        }

        PlayerState.Save(updated);
        return QuestBattleAdmissionReply();
    }

    internal LocalReply QuestBattleAdmissionReply() => new(0, new() { ["user_update"] = new JsonObject { ["phantasm_point"] = PlayerState.user["phantasm_point"]!.DeepClone() } });
    internal LocalReply EndQuestBattle(LocalQuestBattleEnd? request, LocalDailyQuestDay? day)
    {
        if (request is null || request.BattleId <= 0 || request.Result is not (0 or 1))
            return new(400, null, "Invalid quest battle result.");
        var pending = PlayerState.user[PendingQuestBattle];
        var episodeId = PendingQuestBattleEpisode;
        var difficulty = PendingQuestBattleDifficulty;
        var isDaily = PendingDailyQuestStage > 0;
        if ((!isDaily && episodeId <= 0) || difficulty is < 0 or > 2 || pending?["battle_id"]?.GetValue<int>() != request.BattleId)
            return new(409, null, "Matching quest battle entry required.");
        var participants = pending["participants"]!.AsArray().Select(id => id!.GetValue<long>()).ToHashSet();
        if (request.Defeated.Distinct().Count() != request.Defeated.Length || request.Defeated.Any(id => !participants.Contains(id)))
            return new(400, null, "Invalid defeated battle members.");
        var won = request.Result == 1;
        var rewards = request.Rewards;
        if (won && (rewards is null || rewards.GoldMin < 0 || rewards.GoldMax < rewards.GoldMin || rewards.GoldCapacity <= 0 || rewards.PlayerExp < 0 || rewards.UnitExp < 0 || rewards.PlayerLevels.Length == 0 || rewards.Units.Any(unit => unit.Levels.Length == 0) || !rewards.Units.Select(unit => unit.UnitId).SequenceEqual(PendingQuestBattleOwnedUnits)))
            return new(503, null, "Quest battle reward definitions unavailable.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var challenges = new JsonArray();
        var drops = new JsonArray();
        var changes = new JsonObject();
        if (won && rewards is not null)
        {
            var points = updated["phantasm_point"]!["points"]!.GetValue<int>();
            var cost = pending["cost"]!.GetValue<int>();
            if (points < cost)
                return new(409, null, "Not enough spirit points.");
            updated["phantasm_point"]!["points"] = points - cost;
            changes["phantasm_point"] = updated["phantasm_point"]!.DeepClone();
            if (!isDaily && request.ClearScore is { } score && participants.SetEquals(PendingQuestBattleOwnedUnits))
                RecordQuestClear(updated, request.BattleId, PendingQuestBattleOwnedUnits, score);
            updated[QuestWinCount] = (int)Math.Min((long)QuestWins() + 1, int.MaxValue);
            if (day is not null && updated[DailyQuestDay]?.GetValue<int>() == day.Date)
                updated[DailyBattleWins] = (int)Math.Min((long)(updated[DailyBattleWins]?.GetValue<int>() ?? 0) + 1, int.MaxValue);
            for (var index = 0; index < rewards.DefeatLimits.Length; index++)
                if (request.Defeated.Length <= rewards.DefeatLimits[index])
                    challenges.Add(index + 1);
            var gold = updated["gold"]!.GetValue<int>();
            var rolledGold = (int)PlayerState.Random.NextInt64(rewards.GoldMin, (long)rewards.GoldMax + 1);
            var earned = Math.Min(rolledGold, Math.Max(0, rewards.GoldCapacity - gold));
            updated["gold"] = gold + earned;
            if (earned > 0)
                drops.Add(new JsonObject { ["object_type"] = 3, ["object_id"] = 0, ["object_value"] = earned });
            AddBattleExperience(updated, rewards.PlayerExp, rewards.PlayerLevels);
            var units = updated["unit"]!["unit_list"]!.AsArray();
            foreach (var growth in rewards.Units)
            {
                var unit = units.FirstOrDefault(row => row?["user_unit_id"]?.GetValue<long>() == growth.UnitId);
                if (unit is null)
                    return new(409, null, "Battle participant is no longer owned.");
                AddBattleExperience(unit.AsObject(), rewards.UnitExp, growth.Levels);
            }

            foreach (var key in new[]
            {
                "gold",
                "exp",
                "level",
                "unit"
            }

            )
                changes[key] = updated[key]!.DeepClone();
        }

        var resultState = won ? (challenges.Count == 3 ? 3 : 2) : 1;
        if (isDaily)
            UpdateDailyQuestProgress(updated, pending.AsObject(), resultState, challenges, won, changes);
        else
            UpdateStoryBattleProgress(updated, episodeId, difficulty, resultState, challenges, changes);
        updated.Remove(PendingQuestBattle);
        PlayerState.Save(updated);
        return new(0, new() { ["drop_list"] = drops, ["destiny_drop_list"] = new JsonArray(), ["battle_challenge_result"] = challenges.Count, ["user_update"] = changes });
    }

    internal static void UpdateStoryBattleProgress(JsonObject updated, int episodeId, int difficulty, int state, JsonArray challenges, JsonObject changes)
    {
        var episodes = updated["quest_playdata"] as JsonArray ?? new JsonArray();
        var episode = episodes.FirstOrDefault(row => row?["episode_id"]?.GetValue<int>() == episodeId) as JsonObject;
        if (episode is null)
        {
            episode = new JsonObject
            {
                ["episode_id"] = episodeId,
                ["scenario_status"] = 0
            };
            episodes.Add(episode);
        }

        foreach (var key in QuestDifficulties)
            episode[key] ??= new JsonObject
            {
                ["status"] = -2,
                ["battle_challenge"] = new JsonArray()
            };
        SetQuestProgress(episode[QuestDifficulties[difficulty]]!, state, challenges);
        if (episodes.Parent is null)
            updated["quest_playdata"] = episodes;
        changes["quest_playdata"] = episodes.DeepClone();
    }

    internal static void SetQuestProgress(JsonNode entry, int state, JsonArray challenges)
    {
        var previous = entry["battle_challenge"] as JsonArray;
        if (previous is null || challenges.Count > previous.Count)
            entry["battle_challenge"] = challenges.DeepClone();
        entry["status"] = Math.Max(entry["status"]?.GetValue<int>() ?? -2, state);
    }

    internal static bool RefundInterruptedQuest(JsonObject updated)
    {
        if (updated[PendingQuestBattle] is not JsonObject pending || pending[ChargeQuestOnWin]?.GetValue<bool>() == true || pending["cost"]?.GetValue<int>() is not >= 0)
            return false;
        var balance = updated["phantasm_point"]!;
        balance["points"] = (int)Math.Min(int.MaxValue, (long)balance["points"]!.GetValue<int>() + pending["cost"]!.GetValue<int>());
        updated.Remove(PendingQuestBattle);
        return true;
    }

    internal static void AddBattleExperience(JsonObject entity, int amount, LocalBattleLevel[] levels)
    {
        var exp = entity["exp"]!.GetValue<long>();
        var next = exp + Math.Min(amount, Math.Max(0L, levels.Max(row => row.Exp) - exp));
        entity["exp"] = next;
        var level = entity["level"]!.GetValue<int>();
        entity["level"] = levels.Where(row => row.Exp <= next).Select(row => row.Level).Append(level).Max();
    }

    internal LocalReply QuestReplay(QuestReplayCommand? arguments, bool write)
    {
        var target = arguments?.ReplayMeasure;
        if (target is null || target.QuestType is < 0 or > 4 || target.TargetId <= 0 || target.Difficulty < 0)
            return new(400, null, "Valid replay target required.");
        var key = string.Create(CultureInfo.InvariantCulture, $"{target.QuestType}:{target.TargetId}:{target.Difficulty}");
        if (!write)
            return new(0, new() { ["replay_info"] = PlayerState.user["_local_quest_replays"]?[key]?.GetValue<string>() ?? "" });
        if (arguments!.Text is null)
            return new(400, null, "Replay content required.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var records = updated["_local_quest_replays"] as JsonObject ?? new JsonObject();
        if (records.Parent is null)
            updated["_local_quest_replays"] = records;
        if (arguments.Text.Length == 0)
            records.Remove(key);
        else
            records[key] = arguments.Text;
        PlayerState.Save(updated);
        return new(0, new());
    }

    internal LocalReply SaveSeriesReplay(SaveSeriesReplayCommand? request)
    {
        if (request?.EntityId is null or <= 0 or > int.MaxValue || request.Value is null or < 1 or > 5 || request.Text is null)
            return new(400, null, "Stage, area index 1 through 5 and replay content required.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var records = updated["_local_series_replays"] as JsonObject ?? new JsonObject();
        if (records.Parent is null)
            updated["_local_series_replays"] = records;
        var key = string.Create(CultureInfo.InvariantCulture, $"{request.EntityId}:{request.Value}");
        if (request.Text.Length == 0)
            records.Remove(key);
        else
            records[key] = request.Text;
        PlayerState.Save(updated);
        return new(0, new());
    }

    internal LocalReply GetSeriesReplays(GetSeriesReplaysCommand? request)
    {
        if (request?.EntityId is null or <= 0 or > int.MaxValue || request.Ids is null || request.Ids.Any(area => area is < 1 or > 5))
            return new(400, null, "Stage and area indices 1 through 5 required.");
        var body = new JsonObject();
        for (var area = 1; area <= 5; area++)
        {
            var key = string.Create(CultureInfo.InvariantCulture, $"{request.EntityId}:{area}");
            body["replay_info" + area] = request.Ids.Contains(area) ? PlayerState.user["_local_series_replays"]?[key]?.GetValue<string>() ?? "" : "";
        }

        return new(0, body);
    }

    internal LocalReply SaveReplay(LocalReplayContent? request)
    {
        if (request is null || request.TargetId <= 0 || request.Level < 0 || request.Content is null || request.Rules is null)
            return new(400, null, "Replay target, level, rules and content required.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var records = updated["_local_weekly_replays"] as JsonObject ?? new JsonObject();
        if (records.Parent is null)
            updated["_local_weekly_replays"] = records;
        var key = request.TargetId.ToString(CultureInfo.InvariantCulture);
        if (request.Content.Length == 0)
            records.Remove(key);
        else
            records[key] = new JsonObject
            {
                ["level"] = request.Level,
                ["add_rules"] = request.Rules,
                ["replay_info"] = request.Content
            };
        PlayerState.Save(updated);
        return new(0, new());
    }

    internal LocalReply GetStoredReplay(long? target)
    {
        if (target is null or <= 0 or > int.MaxValue)
            return new(400, null, "Valid replay target required.");
        var key = target.Value.ToString(CultureInfo.InvariantCulture);
        return new(0, PlayerState.user["_local_weekly_replays"]?[key]?.DeepClone().AsObject() ?? new JsonObject { ["level"] = 0, ["add_rules"] = "", ["replay_info"] = "" });
    }

    internal LocalReply ReplayMeasurement(LocalReplayMeasure? request, bool write)
    {
        if (request is null || request.QuestType is < 0 or > 4 || request.TargetId <= 0 || request.Difficulty < 0)
            return new(400, null, "Valid replay target required.");
        var key = string.Create(CultureInfo.InvariantCulture, $"{request.QuestType}:{request.TargetId}:{request.Difficulty}");
        if (!write)
            return new(0, PlayerState.user["_local_replay_measurements"]?[key]?.DeepClone().AsObject() ?? new JsonObject { ["win_rate"] = 0, ["action_num"] = 0 });
        if (request.WinRate is null or < 0 || request.ActionNum is null or < 0)
            return new(400, null, "Nonnegative replay measurements required.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var records = updated["_local_replay_measurements"] as JsonObject ?? new JsonObject();
        if (records.Parent is null)
            updated["_local_replay_measurements"] = records;
        records[key] = new JsonObject
        {
            ["win_rate"] = request.WinRate.Value,
            ["action_num"] = request.ActionNum.Value
        };
        PlayerState.Save(updated);
        return new(0, new());
    }
}
