using System.Text.Json.Nodes;
using System.Text;
using System.Text.Json;
using System.Globalization;
using System.Security.Cryptography;

namespace TouhouLastWorld;

internal sealed class BattleModesService
{
    internal BattleModesService(PlayerState playerState)
    {
        PlayerState = playerState;
    }

    private readonly PlayerState PlayerState;
    internal LocalReply DailyOverview()
    {
        if (PlayerState.user["_local_daily_arena"] is not JsonObject overview || overview["custom_battle_id"]?.GetValue<int>() is not > 0 || overview["enemy_info"] is not JsonArray { Count: > 0 } waves)
            return new(503, null, "Local daily configuration unavailable.");
        if (waves.Any(wave => wave is not JsonObject || wave["enemy1"] is not JsonObject || wave["enemy2"] is not JsonObject || wave["enemy3"] is not JsonObject))
            return new(503, null, "Local daily enemy configuration incomplete.");
        var body = new JsonObject
        {
            ["enemy_info"] = waves.DeepClone()
        };
        foreach (var field in new[]
        {
            "custom_battle_id",
            "clear_diffculty",
            "reward_day_count",
            "enemy_level",
            "last_clear_difficulty"
        }

        )
            body[field] = overview[field]?.DeepClone() ?? JsonValue.Create(0);
        return new(0, body);
    }

    internal LocalReply WeeklyOverview()
    {
        if (PlayerState.user["_local_weekly_arena"] is not JsonObject overview || overview["custom_battle_id"]?.GetValue<int>() is not > 0 || overview["weekly_battle_data"] is not JsonObject battles)
            return new(503, null, "Local weekly configuration unavailable.");
        for (var number = 1; number <= 4; number++)
        {
            if (battles[$"battle_number{number}"] is not JsonObject battle || battle["buff_role_type"] is not JsonArray || battle["wave_enemy_info"] is not JsonArray || battle["unit_list"] is not JsonArray || battle["user_unit_list"] is not JsonArray)
                return new(503, null, "Local weekly battle configuration incomplete.");
        }

        return new(0, new() { ["custom_battle_id"] = overview["custom_battle_id"]!.DeepClone(), ["weekly_battle_data"] = battles.DeepClone(), ["total_battle_score"] = overview["total_battle_score"]?.DeepClone() ?? JsonValue.Create(0) });
    }

    internal LocalReply ResetWeeklyScore(int? number)
    {
        if (number is null or < 1 or > 4)
            return new(400, null, "Weekly battle number must be 1 to 4.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var overview = updated["_local_weekly_arena"] as JsonObject;
        var battles = overview?["weekly_battle_data"] as JsonObject;
        if (battles?[$"battle_number{number}"] is not JsonObject battle)
            return new(404, null, "Local weekly battle not found.");
        battle["total_score"] = 0;
        battle["unit_list"] = new JsonArray();
        battle["user_unit_list"] = new JsonArray();
        PlayerState.Save(updated);
        return new(0, new());
    }

    internal LocalReply ResetSeries()
    {
        var updated = PlayerState.user.DeepClone().AsObject();
        var state = updated["series_battle_status"]!.AsObject();
        foreach (var field in new[]
        {
            "episode_id",
            "difficulty",
            "progress",
            "stage_group_master_id",
            "manage_id",
            "value1"
        }

        )
            state[field] = 0;
        state["use_replays"] = new JsonArray();
        updated.Remove("_local_series_pending");
        PlayerState.Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["series_battle_status"] = state.DeepClone() } });
    }

    internal int PendingArenaCategory => PlayerState.user["_local_arena_pending"]?["category"]?.GetValue<int>() ?? 0;
    internal int DailyArenaRewardDay => PlayerState.user["_local_daily_arena"]?["reward_day_count"]?.GetValue<int>() ?? -1;

    internal LocalReply EndArena(LocalArenaEnd? rule)
    {
        if (rule is null || rule.Result is not (0 or 1) || rule.ExtraTeams)
            return new(400, null, "Invalid battle result.");
        if (PlayerState.user["_local_arena_pending"] is not JsonObject pending || pending["battle_number"]?.GetValue<int>() != rule.BattleNumber || pending["unit_ids"] is not JsonArray members)
            return new(409, null, "Matching battle entry unavailable.");
        var ids = members.Select(row => row!.GetValue<long>()).Where(id => id > 0).ToArray();
        if (rule.Defeated.Any(id => id <= 0 || !ids.Contains(id)) || rule.Defeated.Distinct().Count() != rule.Defeated.Length)
            return new(400, null, "Invalid defeated members.");
        var category = pending["category"]?.GetValue<int>() ?? 0;
        if (category is not (1 or 2))
            return new(409, null, "Invalid pending category.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var rewards = new JsonArray();
        if (rule.Result == 1 && category == 1)
        {
            var daily = updated["_local_daily_arena"];
            var difficulty = pending["difficulty"]!.GetValue<int>();
            var cleared = daily?["clear_diffculty"]?.GetValue<int>();
            if (daily?["custom_battle_id"]?.GetValue<int>() != pending["custom_battle_id"]!.GetValue<int>() || cleared is null or < 0 or > 5 || difficulty is < 1 or > 5 || rule.Daily is null || rule.Daily.Day < 0 || daily?["reward_day_count"]?.GetValue<int>() != rule.Daily.Day || rule.Daily.Stages.Select(row => row.Difficulty).Distinct().Count() != rule.Daily.Stages.Length)
                return new(503, null, "Daily reward definitions unavailable.");
            for (var stage = cleared.Value + 1; stage <= difficulty; stage++)
            {
                var stageRewards = rule.Daily.Stages.SingleOrDefault(row => row.Difficulty == stage)?.Rewards;
                if (stageRewards is null || stageRewards.Length == 0)
                    return new(503, null, "Daily stage rewards unavailable.");
                foreach (var reward in stageRewards)
                {
                    if (reward.Type == 21)
                    {
                        var points = updated["arena_point"]?.GetValue<int>();
                        if (reward.Count <= 0 || reward.Capacity <= 0)
                            return new(503, null, "Point reward definition unavailable.");
                        if (points is null or < 0 || (long)points + reward.Count > reward.Capacity)
                            return new(409, null, "Reward exceeds point capacity.");
                        updated["arena_point"] = points.Value + reward.Count;
                    }
                    else
                    {
                        var error = EconomyService.ApplyInventoryReward(updated, reward);
                        if (error is not null)
                            return error;
                    }

                    rewards.Add(new JsonObject { ["object_type"] = reward.Type, ["object_id"] = reward.Id, ["object_value"] = reward.Count, ["rate_up_flg"] = 0, ["point_up_flg"] = 0 });
                }
            }

            daily!["clear_diffculty"] = Math.Max(cleared.Value, difficulty);
            daily["last_clear_difficulty"] = difficulty;
        }

        if (rule.Result == 1 && category == 2)
        {
            if (rule.BattleNumber is < 1 or > 4 || rule.Score is not { AttackCount: >= 0, GrazeCount: >= 0 } score || rule.Rewards is null || rule.Capacity <= 0 || rule.Rewards.Any(row => row.Id <= 0 || row.Score < 0 || row.Value < 0) || rule.Rewards.Select(row => row.Id).Distinct().Count() != rule.Rewards.Length)
                return new(503, null, "Weekly score or reward definitions unavailable.");
            var difficulty = pending["difficulty"]!.GetValue<int>();
            var attack = score.AttackCount is >= 1 and <= 5 ? 24 : score.AttackCount is >= 6 and <= 16 ? 34 - 2 * score.AttackCount : 0;
            var graze = (int)Math.Max(0, 20L - 2L * score.GrazeCount);
            var defeated = Math.Max(0, 20 - 10 * rule.Defeated.Length);
            var total = attack + graze + defeated + (difficulty == 3 ? 15 : difficulty == 4 ? 36 : 0);
            if (score.Total != total || score.Attack != attack || score.Graze != graze || score.Defeated != defeated)
                return new(409, null, "Score does not match battle counters.");
            var overview = updated["_local_weekly_arena"];
            if (overview?["custom_battle_id"]?.GetValue<int>() != pending["custom_battle_id"]!.GetValue<int>() || overview?["weekly_battle_data"] is not JsonObject battles || overview["total_battle_score"]?.GetValue<int>() is not >= 0)
                return new(409, null, "Weekly progress unavailable.");
            for (var number = 1; number <= 4; number++)
                if (battles[$"battle_number{number}"]?["total_score"]?.GetValue<int>() is not >= 0)
                    return new(409, null, "Invalid weekly score history.");
            var battle = battles[$"battle_number{rule.BattleNumber}"]!;
            if (total >= battle["total_score"]!.GetValue<int>())
            {
                var owned = updated["unit"]!["unit_list"]!.AsArray();
                if (ids.Any(id => owned.Count(row => row?["user_unit_id"]?.GetValue<long>() == id) != 1))
                    return new(409, null, "Participating member unavailable.");
                battle["is_clear"] = 1;
                battle["difficulty"] = difficulty;
                battle["total_score"] = total;
                battle["attack_count_score"] = attack;
                battle["graze_count_score"] = graze;
                battle["defeated_score"] = defeated;
                battle["user_unit_list"] = new JsonArray(ids.Select(id => (JsonNode)JsonValue.Create(id)!).ToArray());
                battle["unit_list"] = new JsonArray(ids.Select(id => owned.Single(row => row?["user_unit_id"]?.GetValue<long>() == id)!["unit_id"]!.DeepClone()).ToArray());
            }

            var previous = overview["total_battle_score"]!.GetValue<int>();
            var combined = Enumerable.Range(1, 4).Sum(number => (long)battles[$"battle_number{number}"]!["total_score"]!.GetValue<int>());
            if (combined > int.MaxValue)
                return new(409, null, "Invalid combined score.");
            var best = Math.Max(previous, (int)combined);
            var grant = rule.Rewards.Where(row => row.Score > previous && row.Score <= best).Sum(row => (long)row.Value);
            var balance = updated["arena_point"]?.GetValue<int>();
            if (balance is null or < 0 || balance + grant > rule.Capacity)
                return new(409, null, "Reward exceeds point capacity.");
            updated["arena_point"] = balance.Value + (int)grant;
            overview["total_battle_score"] = best;
            if (grant > 0)
                rewards.Add(new JsonObject { ["object_type"] = 21, ["object_id"] = 0, ["object_value"] = (int)grant, ["rate_up_flg"] = 0, ["point_up_flg"] = 0 });
        }

        updated.Remove("_local_arena_pending");
        PlayerState.Save(updated);
        return new(0, new() { ["user_arena"] = new JsonObject { ["daily_cleared_difficulty"] = updated["_local_daily_arena"]?["clear_diffculty"]?.DeepClone() ?? JsonValue.Create(0) }, ["first_cleared_reward_list"] = rewards, ["user_update"] = new JsonObject { ["arena_point"] = updated["arena_point"]!.DeepClone(), ["gold"] = updated["gold"]!.DeepClone(), ["crystal"] = updated["crystal"]!.DeepClone(), ["item"] = updated["item"]!.DeepClone() } });
    }

    internal LocalReply StartArena(LocalArenaStart? rule)
    {
        if (rule is null)
            return new(503, null, "Battle definition unavailable.");
        if (rule.Category is not (1 or 2) || rule.BattleId <= 0 || rule.EnemyLevel < 0 || rule.Difficulty < 0 || rule.Teams.Length != 1 || rule.Teams[0] <= 0 || rule.Category == 2 && (rule.BattleNumber is < 1 or > 4 || rule.Teams[0] != rule.BattleNumber + 59))
            return new(400, null, "Invalid battle entry.");
        var overviewReply = rule.Category == 1 ? DailyOverview() : WeeklyOverview();
        if (overviewReply.Status != 0)
            return overviewReply;
        if (overviewReply.Body!["custom_battle_id"]!.GetValue<int>() != rule.BattleId)
            return new(409, null, "Battle configuration changed.");
        var overview = PlayerState.user[rule.Category == 1 ? "_local_daily_arena" : "_local_weekly_arena"]!;
        var definitions = (overview["entry_rules"] as JsonArray)?.Where(row => row?["difficulty"]?.GetValue<int>() == rule.Difficulty && row?["enemy_level"]?.GetValue<int>() == rule.EnemyLevel && row?["battle_number"]?.GetValue<int>() == rule.BattleNumber).ToArray();
        if (definitions is not { Length: 1 } || definitions[0]?["cost"]?.GetValue<int>() is not >= 0)
            return new(503, null, "Local entry rule unavailable.");
        var cost = definitions[0]!["cost"]!.GetValue<int>();
        var matches = PlayerState.user["team"]!["team_list"]!.AsArray().Where(row => row?["number"]?.GetValue<int>() == rule.Teams[0]).ToArray();
        if (matches.Length != 1 || matches[0]?["user_unit_id_list"] is not JsonArray members || members.Count is < 1 or > 6)
            return new(409, null, "Team unavailable.");
        var ids = members.Select(row => row!.GetValue<long>()).ToArray();
        var positive = ids.Where(id => id > 0).ToArray();
        var units = PlayerState.user["unit"]!["unit_list"]!.AsArray();
        if (positive.Length == 0 || ids.Any(id => id < 0) || positive.Distinct().Count() != positive.Length || positive.Any(id => units.Count(row => row?["user_unit_id"]?.GetValue<long>() == id) != 1))
            return new(409, null, "Invalid team members.");
        var unitTypes = positive.Select(id => units.Single(row => row?["user_unit_id"]?.GetValue<long>() == id)!["unit_id"]!.GetValue<int>()).ToArray();
        if (rule.Category == 2)
        {
            var battles = overview["weekly_battle_data"]!;
            for (var number = 1; number <= 4; number++)
            {
                if (number == rule.BattleNumber)
                    continue;
                var reserved = battles[$"battle_number{number}"]!["unit_list"]!.AsArray();
                if (reserved.Any(row => row is not null && unitTypes.Contains(row.GetValue<int>())))
                    return new(409, null, "Unit is assigned to another battle.");
            }
        }

        var pending = new JsonObject
        {
            ["category"] = rule.Category,
            ["custom_battle_id"] = rule.BattleId,
            ["enemy_level"] = rule.EnemyLevel,
            ["difficulty"] = rule.Difficulty,
            ["battle_number"] = rule.BattleNumber,
            ["team_number"] = rule.Teams[0],
            ["unit_ids"] = members.DeepClone(),
            ["cost"] = cost
        };
        if (PlayerState.user["_local_arena_pending"] is JsonObject previous)
            return JsonNode.DeepEquals(previous, pending) ? new(0, new() { ["user_update"] = new JsonObject { ["phantasm_point"] = PlayerState.user["phantasm_point"]!.DeepClone() } }) : new(409, null, "Another entry is pending.");
        var points = PlayerState.user["phantasm_point"]?["points"]?.GetValue<int>();
        if (points is null or < 0 || points < cost)
            return new(409, null, "Insufficient entry points.");
        var updated = PlayerState.user.DeepClone().AsObject();
        updated["phantasm_point"]!["points"] = points.Value - cost;
        updated["_local_arena_pending"] = pending;
        PlayerState.Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["phantasm_point"] = updated["phantasm_point"]!.DeepClone() } });
    }

    internal LocalReply BatchSearch(LocalBatchSearch? rule)
    {
        if (rule is null)
            return new(503, null, "Batch definitions unavailable.");
        if (rule.Quests.Length == 0 || rule.Quests.Any(row => row.Id <= 0 || row.UnitId <= 0 || row.Limit <= 0) || rule.Quests.Select(row => row.Id).Distinct().Count() != rule.Quests.Length)
            return new(400, null, "Invalid batch selection.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var saved = updated["character_quest_list"] as JsonArray;
        var plans = updated["_local_batch_search_rewards"] as JsonObject;
        var drops = new JsonArray();
        long cost = 0;
        foreach (var quest in rule.Quests)
        {
            if (!quest.Eligible || !updated["unit"]!["unit_list"]!.AsArray().Any(row => row?["unit_id"]?.GetValue<int>() == quest.UnitId))
                return new(409, null, "Batch entry is locked.");
            var matches = saved?.Where(row => row?["character_quest_id"]?.GetValue<int>() == quest.Id).ToArray();
            if (matches is not { Length: 1 } || matches[0] is not JsonObject progress || progress["lunatic"]?["status"]?.GetValue<int>() != 3)
                return new(409, null, "Completed challenge required.");
            var used = progress["count"]?.GetValue<int>();
            if (used is null or < 0 || used >= quest.Limit)
                return new(409, null, "No remaining attempts.");
            var remaining = quest.Limit - used.Value;
            var plan = plans?[quest.Id.ToString()];
            if (plan?["count"]?.GetValue<int>() != used || plan?["runs"] is not JsonArray runs || runs.Count != remaining)
                return new(503, null, "Matching local batch rewards required.");
            foreach (var run in runs)
            {
                if (run is not JsonArray rewards)
                    return new(503, null, "Invalid batch reward outcome.");
                foreach (var reward in rewards)
                {
                    var type = reward?["object_type"]?.GetValue<int>() ?? 0;
                    var id = reward?["object_id"]?.GetValue<int>() ?? 0;
                    var count = reward?["object_value"]?.GetValue<int>() ?? 0;
                    var allowed = rule.Rewards.Where(row => row.Type == type && row.Id == id).ToArray();
                    if (allowed.Length != 1)
                        return new(503, null, "Batch reward unsupported.");
                    var error = EconomyService.ApplyInventoryReward(updated, allowed[0] with { Count = count });
                    if (error is not null)
                        return error;
                    drops.Add(new JsonObject { ["object_type"] = type, ["object_id"] = id, ["object_value"] = count, ["is_eff"] = false, ["custom_parameter"] = "" });
                }
            }

            cost += (long)remaining * 12;
            progress["count"] = quest.Limit;
            plans!.Remove(quest.Id.ToString());
        }

        var points = updated["phantasm_point"]?["points"]?.GetValue<int>();
        if (points is null or < 0 || cost > points)
            return new(409, null, "Insufficient batch points.");
        updated["phantasm_point"]!["points"] = points.Value - (int)cost;
        PlayerState.Save(updated);
        return new(0, new() { ["drop_list"] = drops, ["user_update"] = new JsonObject { ["character_quest_list"] = updated["character_quest_list"]!.DeepClone(), ["phantasm_point"] = updated["phantasm_point"]!.DeepClone(), ["gold"] = updated["gold"]!.DeepClone(), ["crystal"] = updated["crystal"]!.DeepClone(), ["item"] = updated["item"]!.DeepClone() } });
    }

    internal LocalReply Reroll(LocalReroll? rule)
    {
        if (rule is null)
            return new(503, null, "Draw definitions unavailable.");
        if (rule.Selected != 0)
            return new(503, null, "Effect redraw unavailable.");
        var state = PlayerState.user["_local_rogue_draw"];
        if (state?["drawn_seals"] is not JsonArray offered || offered.Count == 0 || state["remaining_draws"]?.GetValue<int>() is not > 0 || state["remaining_rerolls"]?.GetValue<int>() is not > 0)
            return new(409, null, "Active draw with remaining rerolls required.");
        if (!offered.Select(id => id!.GetValue<int>()).SequenceEqual(rule.Offered) || rule.Draws != state["remaining_draws"]!.GetValue<int>() || rule.Rerolls != state["remaining_rerolls"]!.GetValue<int>())
            return new(409, null, "Draw state changed.");
        if (PlayerState.user["_local_rogue_reroll_sequence"] is not JsonArray sequence || sequence.Count == 0 || sequence[0] is not JsonArray next || next.Count != offered.Count || next.Any(id => id is null || id.GetValue<int>() <= 0 || !rule.Definitions.Contains(id.GetValue<int>())) || offered.Any(id => id is null || !rule.Definitions.Contains(id.GetValue<int>())))
            return new(503, null, "Prepared draw outcome unavailable.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var result = new JsonObject
        {
            ["drawn_seals"] = next.DeepClone(),
            ["remaining_draws"] = rule.Draws,
            ["remaining_rerolls"] = rule.Rerolls - 1
        };
        updated["_local_rogue_draw"] = result.DeepClone();
        updated["_local_rogue_reroll_sequence"]!.AsArray().RemoveAt(0);
        PlayerState.Save(updated);
        return new(0, result);
    }

    internal LocalReply MiniGameOverview(MiniGameOverviewCommand? request)
    {
        if (request?.Ids is not { Length: > 0 } ids || ids.Any(id => id <= 0 || id > int.MaxValue) || ids.Distinct().Count() != ids.Length)
            return new(503, null, "Entry definitions unavailable.");
        var remaining = PlayerState.user["_local_mini_game_remaining_points"]?.GetValue<int>() ?? 0;
        if (remaining < 0)
            return new(503, null, "Remaining points are invalid.");
        var flags = PlayerState.user["_local_mini_game_claims"] as JsonObject;
        var entries = new JsonArray();
        foreach (var id in ids)
        {
            var flag = flags?[id.ToString(System.Globalization.CultureInfo.InvariantCulture)]?.GetValue<int>() ?? 0;
            if (flag is < 0 or > 1)
                return new(503, null, "Reward claim state is invalid.");
            entries.Add(new JsonObject { ["mini_game_master_id"] = (int)id, ["get_reward_flg"] = flag });
        }

        return new(0, new() { ["first_clear_reward_info"] = entries, ["get_phantasm_point"] = remaining });
    }

    internal LocalReply EndRhythm(LocalRhythmResult? result)
    {
        if (result is null || result.MusicId <= 0 || result.Difficulty is < 2 or > 4 || result.Score < 0 || !float.IsFinite(result.Rate) || result.Rate < 0 || result.Rank < 0)
            return new(400, null, "Invalid rhythm result.");
        if (PlayerState.user["_local_rhythm_active"]?.GetValue<int>() != result.MusicId)
            return new(409, null, "No matching active music.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var scores = updated["_local_rhythm_scores"] as JsonArray ?? new JsonArray();
        if (scores.Parent is null)
            updated["_local_rhythm_scores"] = scores;
        var entry = scores.FirstOrDefault(value => value!["rhythm_game_music_master_id"]!.GetValue<int>() == result.MusicId && value["difficulty"]!.GetValue<int>() == result.Difficulty);
        if (entry is null)
        {
            entry = new JsonObject
            {
                ["rhythm_game_music_master_id"] = result.MusicId,
                ["difficulty"] = result.Difficulty,
                ["highest_score"] = 0,
                ["highest_rate"] = 0f,
                ["rank"] = 0
            };
            scores.Add(entry);
        }

        entry["highest_score"] = Math.Max(entry["highest_score"]!.GetValue<int>(), result.Score);
        entry["highest_rate"] = Math.Max(entry["highest_rate"]!.GetValue<float>(), result.Rate);
        entry["rank"] = Math.Max(entry["rank"]!.GetValue<int>(), result.Rank);
        updated.Remove("_local_rhythm_active");
        PlayerState.Save(updated);
        return new(0, new() { ["remaining_phantasm_point"] = 0, ["reward_list"] = new JsonArray() });
    }

    internal LocalReply StartRhythm(StartRhythmCommand? request)
    {
        if (request?.EntityId is null or <= 0 or > int.MaxValue)
            return new(400, null, "Positive music ID required.");
        if (request.Value != 1)
            return new(404, null, "Music definition unavailable.");
        var updated = PlayerState.user.DeepClone().AsObject();
        updated["_local_rhythm_active"] = (int)request.EntityId.Value;
        PlayerState.Save(updated);
        return new(0, new());
    }

    internal LocalReply StartSeries(LocalSeriesStart? rule)
    {
        if (rule is null || rule.EpisodeId <= 0 || rule.Difficulty is < 0 or > 2 || rule.GroupId <= 0 || rule.StageId <= 0 || rule.AreaCount is < 1 or > 5)
            return new(503, null, "Series definition unavailable.");
        if (!rule.Active)
            return new(1008, null, "Series is not open.");
        var state = PlayerState.user["series_battle_status"]!.AsObject();
        var progress = state["progress"]!.GetValue<int>();
        if (progress < 0 || progress > rule.AreaCount)
            return new(409, null, "Invalid series progress.");
        if (progress > 0 && (state["episode_id"]!.GetValue<int>() != rule.EpisodeId || state["difficulty"]!.GetValue<int>() != rule.Difficulty || state["manage_id"]!.GetValue<int>() != 0))
            return new(409, null, "Another series is active.");
        var replays = rule.Replays ?? (progress > 0 ? state["use_replays"]!.AsArray().Select(value => value!.GetValue<int>()).ToArray() : new int[rule.AreaCount]);
        if (replays.Length < rule.AreaCount || replays.Length > 5 || replays.Any(value => value is not (0 or 1)) || replays.Skip(rule.AreaCount).Any(value => value != 0))
            return new(400, null, "Invalid replay selection.");
        if (progress > 0 && !state["use_replays"]!.AsArray().Select(value => value!.GetValue<int>()).SequenceEqual(replays))
            return new(409, null, "Active replay selection cannot change.");
        var owned = PlayerState.user["unit"]!["unit_list"]!.AsArray().Select(row => row!["user_unit_id"]!.GetValue<long>()).ToHashSet();
        var teams = PlayerState.user["team"]!["team_list"]!.AsArray();
        for (var area = Math.Max(progress, 1); area <= rule.AreaCount; area++)
        {
            if (replays[area - 1] == 1)
            {
                var key = string.Create(CultureInfo.InvariantCulture, $"{rule.StageId}:{area}");
                if (string.IsNullOrEmpty(PlayerState.user["_local_series_replays"]?[key]?.GetValue<string>()))
                    return new(409, null, "Selected replay is unavailable.");
                continue;
            }

            var matching = teams.Where(row => row?["number"]?.GetValue<int>() == 79 + area).ToArray();
            if (matching.Length != 1 || matching[0]?["user_unit_id_list"] is not JsonArray members || members.Count != 6)
                return new(409, null, "Series team is unavailable.");
            var ids = members.Select(value => value!.GetValue<long>()).ToArray();
            var positive = ids.Where(id => id > 0).ToArray();
            if (positive.Length == 0 || ids.Any(id => id < 0) || positive.Distinct().Count() != positive.Length || positive.Any(id => !owned.Contains(id)))
                return new(409, null, "Invalid series team members.");
        }

        var updated = PlayerState.user.DeepClone().AsObject();
        if (progress == 0)
        {
            var next = updated["series_battle_status"]!.AsObject();
            next["episode_id"] = rule.EpisodeId;
            next["difficulty"] = rule.Difficulty;
            next["progress"] = 1;
            next["stage_group_master_id"] = rule.GroupId;
            next["manage_id"] = 0;
            next["value1"] = 0;
            next["use_replays"] = new JsonArray(replays.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());
        }

        updated["_local_series_pending"] = new JsonObject
        {
            ["episode_id"] = rule.EpisodeId,
            ["difficulty"] = rule.Difficulty,
            ["progress"] = Math.Max(1, progress)
        };
        PlayerState.Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["series_battle_status"] = PlayerState.user["series_battle_status"]!.DeepClone() } });
    }

    internal LocalReply EndSeries(LocalSeriesEnd? rule)
    {
        if (rule is null || rule.EpisodeId <= 0 || rule.Difficulty is < 0 or > 2 || rule.AreaCount is < 1 or > 5 || rule.Progress < 1 || rule.Progress > rule.AreaCount || rule.Result is not (0 or 1))
            return new(400, null, "Invalid series result.");
        var current = PlayerState.user["series_battle_status"]!;
        var pending = PlayerState.user["_local_series_pending"];
        if (current["episode_id"]!.GetValue<int>() != rule.EpisodeId || current["difficulty"]!.GetValue<int>() != rule.Difficulty || current["progress"]!.GetValue<int>() != rule.Progress || current["manage_id"]!.GetValue<int>() != 0 || pending?["episode_id"]?.GetValue<int>() != rule.EpisodeId || pending?["difficulty"]?.GetValue<int>() != rule.Difficulty || pending?["progress"]?.GetValue<int>() != rule.Progress)
            return new(409, null, "Matching pending series entry required.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var state = updated["series_battle_status"]!.AsObject();
        var drops = new JsonArray();
        if (rule.Result == 1)
        {
            if (rule.Rewards is null)
                return new(503, null, "Area reward definition unavailable.");
            var grants = rule.Rewards.ToList();
            if (rule.Progress == rule.AreaCount)
            {
                var episodes = updated["quest_playdata"] as JsonArray ?? new JsonArray();
                var entries = episodes.Where(row => row?["episode_id"]?.GetValue<int>() == rule.EpisodeId).ToArray();
                if (entries.Length > 1)
                    return new(503, null, "Duplicate episode state.");
                var episode = entries.FirstOrDefault() as JsonObject;
                if (episode is null)
                {
                    episode = new JsonObject
                    {
                        ["episode_id"] = rule.EpisodeId,
                        ["scenario_status"] = 0
                    };
                    foreach (var name in new[]
                    {
                        "normal",
                        "hard",
                        "lunatic"
                    }

                    )
                        episode[name] = new JsonObject
                        {
                            ["status"] = -2,
                            ["battle_challenge"] = new JsonArray()
                        };
                    episodes.Add(episode);
                }

                var difficulty = new[]
                {
                    "normal",
                    "hard",
                    "lunatic"
                }[rule.Difficulty];
                var record = episode[difficulty]!.AsObject();
                var status = record["status"]!.GetValue<int>();
                if (status < 2)
                {
                    if (rule.FirstRewards is null)
                        return new(503, null, "First completion reward definition unavailable.");
                    grants.AddRange(rule.FirstRewards);
                    record["status"] = 2;
                }

                if (episodes.Parent is null)
                    updated["quest_playdata"] = episodes;
                foreach (var name in new[]
                {
                    "episode_id",
                    "difficulty",
                    "progress",
                    "stage_group_master_id",
                    "manage_id",
                    "value1"
                }

                )
                    state[name] = 0;
                state["use_replays"] = new JsonArray();
            }
            else
                state["progress"] = rule.Progress + 1;
            foreach (var grant in grants)
            {
                var error = EconomyService.ApplyInventoryReward(updated, grant);
                if (error is not null)
                    return error;
                drops.Add(new JsonObject { ["object_type"] = grant.Type, ["object_id"] = grant.Id, ["object_value"] = grant.Count, ["rate_up_flg"] = 0, ["point_up_flg"] = 0 });
            }
        }

        updated.Remove("_local_series_pending");
        PlayerState.Save(updated);
        var changes = new JsonObject
        {
            ["series_battle_status"] = updated["series_battle_status"]!.DeepClone(),
            ["gold"] = updated["gold"]!.DeepClone(),
            ["crystal"] = updated["crystal"]!.DeepClone(),
            ["item"] = updated["item"]!.DeepClone()
        };
        if (updated["quest_playdata"] is JsonArray quests)
            changes["quest_playdata"] = quests.DeepClone();
        return new(0, new() { ["drop_list"] = drops, ["destiny_drop_list"] = new JsonArray(), ["user_update"] = changes });
    }

    internal int RemainingTowerRecovery => PlayerState.user["_local_tower_recovery_count"]?.GetValue<int>() ?? 0;

    internal LocalReply RecoverTowerUnits(RecoverTowerUnitsCommand? arguments)
    {
        if (!string.IsNullOrEmpty(arguments?.Text))
            return new(400, null, "Unexpected recovery payload.");
        var remaining = RemainingTowerRecovery;
        if (remaining is < 0 or > 4)
            return new(503, null, "Recovery allowance unavailable.");
        var cost = remaining switch
        {
            1 => 15,
            2 => 10,
            3 => 5,
            _ => 0
        };
        var balance = PlayerState.user["crystal"]?.GetValue<int>() ?? 0;
        var snapshot = TowerUnits();
        if (snapshot.Status != 0)
            return snapshot;
        var hasDamage = !string.IsNullOrEmpty(snapshot.Body!["tower_unit_info"]!.GetValue<string>()) || PlayerState.user["unit"]?["tower_unit_list"] is JsonArray { Count: > 0 };
        if (remaining == 0 || balance < cost || !hasDamage)
            return new(0, new() { ["result"] = 0, ["remaining_recovery_count"] = remaining });
        if (PlayerState.user["unit"] is not JsonObject)
            return new(503, null, "Unit state unavailable.");
        var updated = PlayerState.user.DeepClone().AsObject();
        updated["crystal"] = balance - cost;
        updated["_local_tower_recovery_count"] = remaining - 1;
        updated["_local_tower_unit_info"] = "";
        updated["unit"]!["tower_unit_list"] = new JsonArray();
        PlayerState.Save(updated);
        return new(0, new() { ["result"] = 1, ["remaining_recovery_count"] = remaining - 1, ["user_update"] = new JsonObject { ["crystal"] = updated["crystal"]!.DeepClone(), ["unit"] = updated["unit"]!.DeepClone() } });
    }

    internal LocalReply TowerUnits(JsonNode? supplied = null)
    {
        var snapshot = supplied ?? PlayerState.user["_local_tower_unit_info"];
        if (snapshot == null)
            return new(0, new() { ["tower_unit_info"] = "" });
        if (snapshot is not JsonValue value || !value.TryGetValue<string>(out var csv))
            return new(503, null, "Local unit state unavailable.");
        var ids = new HashSet<long>();
        foreach (var line in csv.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var record = line.TrimEnd('\r');
            if (record.EndsWith(','))
                record = record[..^1];
            var fields = record.Split(',');
            if (fields.Length is not (27 or 29) || !long.TryParse(fields[0], out var id) || id <= 0 || !ids.Add(id) || PlayerState.user["unit"]?["unit_list"] is not JsonArray units || !units.Any(unit => unit?["user_unit_id"]?.GetValue<long>() == id) || fields.Skip(1).Any(field => !int.TryParse(field, out _)))
                return new(503, null, "Invalid local unit state.");
        }

        return new(0, new() { ["tower_unit_info"] = csv });
    }

    internal LocalReply TowerOverview()
    {
        if (PlayerState.user["tower"] is not JsonObject tower || tower["cleared_floor"] is not JsonValue floorValue || !floorValue.TryGetValue<int>(out var floor) || floor < 0 || tower["exist_data"] is not JsonValue existsValue || !existsValue.TryGetValue<uint>(out var exists))
            return new(503, null, "Local floor state unavailable.");
        return new(0, new() { ["cleared_floor"] = floor, ["exist_data"] = exists, ["remaining_recovery_count"] = RemainingTowerRecovery, ["enemy_battle_info"] = PlayerState.user["_local_tower_enemy_info"]?.DeepClone() ?? JsonValue.Create("") });
    }

    internal int PendingTowerId => PlayerState.user["_local_tower_pending"]?["tower_id"]?.GetValue<int>() ?? 0;

    internal LocalReply EndTower(LocalTowerEnd? rule)
    {
        if (rule is null || rule.TowerId <= 0 || rule.BattleId <= 0 || rule.Floor <= 0 || rule.MaxFloor < rule.Floor || rule.Result is not (0 or 1) || rule.Units is null || rule.Enemies is null || rule.Defeated is null)
            return new(400, null, "Invalid floor result.");
        var pending = PlayerState.user["_local_tower_pending"];
        if (pending?["tower_id"]?.GetValue<int>() != rule.TowerId || pending?["battle_id"]?.GetValue<int>() != rule.BattleId || pending?["floor"]?.GetValue<int>() != rule.Floor || pending?["unit_ids"] is not JsonArray members)
            return new(409, null, "Matching pending floor entry required.");
        var cleared = PlayerState.user["tower"]!["cleared_floor"]!.GetValue<int>();
        var repeat = cleared == rule.MaxFloor && rule.Floor == cleared;
        if (!repeat && rule.Floor != (long)cleared + 1)
            return new(409, null, "Floor progress changed.");
        if (rule.Defeated.Distinct().Count() != rule.Defeated.Length || rule.Defeated.Any(id => id <= 0 || !members.Any(value => value!.GetValue<long>() == id)))
            return new(400, null, "Invalid defeated members.");
        var snapshot = TowerUnits(JsonValue.Create(rule.Units));
        if (snapshot.Status != 0)
            return snapshot;
        var snapshotIds = rule.Units.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => long.Parse(line.Split(',')[0])).ToHashSet();
        if (members.Any(value => value!.GetValue<long>() > 0 && !snapshotIds.Contains(value.GetValue<long>())))
            return new(400, null, "Participant snapshot missing.");
        foreach (var line in rule.Enemies.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = line.TrimEnd('\r').Split(',');
            if (fields.Length != 26 || fields.Any(field => !int.TryParse(field, out _)) || !rule.EnemyIds.Contains(int.Parse(fields[0])))
                return new(400, null, "Invalid enemy snapshot.");
        }

        if (rule.Result == 1 && rule.Enemies.Length != 0)
            return new(400, null, "Cleared floor must not retain enemies.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var drops = new JsonArray();
        if (rule.Result == 1)
        {
            var rewards = repeat ? rule.RepeatRewards : rule.FirstRewards;
            if (rewards is null)
                return new(503, null, "Floor reward definition unavailable.");
            foreach (var reward in rewards)
            {
                var error = EconomyService.ApplyInventoryReward(updated, reward);
                if (error is not null)
                    return error;
                drops.Add(new JsonObject { ["object_type"] = reward.Type, ["object_id"] = reward.Id, ["object_value"] = reward.Count, ["rate_up_flg"] = 0, ["point_up_flg"] = 0 });
            }

            updated["tower"]!["cleared_floor"] = Math.Max(cleared, rule.Floor);
        }

        updated["_local_tower_unit_info"] = rule.Units;
        updated["_local_tower_enemy_info"] = rule.Enemies;
        updated.Remove("_local_tower_pending");
        PlayerState.Save(updated);
        return new(0, new() { ["drop_list"] = drops, ["destiny_drop_list"] = new JsonArray(), ["battle_challenge_result"] = 0, ["user_update"] = new JsonObject { ["tower"] = updated["tower"]!.DeepClone(), ["gold"] = updated["gold"]!.DeepClone(), ["crystal"] = updated["crystal"]!.DeepClone(), ["item"] = updated["item"]!.DeepClone() } });
    }

    internal LocalReply StartTower(LocalTowerStart? rule)
    {
        if (rule is null || rule.TowerId <= 0 || rule.BattleId <= 0 || rule.Floor <= 0 || rule.TeamNumber <= 0 || rule.Floor > rule.MaxFloor)
            return new(503, null, "Floor definition unavailable.");
        var cleared = PlayerState.user["tower"]?["cleared_floor"]?.GetValue<int>();
        if (cleared is null or < 0)
            return new(503, null, "Floor progress unavailable.");
        if (rule.Floor != (long)cleared + 1 && !(rule.Floor == cleared && cleared == rule.MaxFloor))
            return new(409, null, "Floor is not unlocked.");
        var snapshot = TowerUnits();
        if (snapshot.Status != 0)
            return snapshot;
        var teams = PlayerState.user["team"]!["team_list"]!.AsArray().Where(row => row?["number"]?.GetValue<int>() == rule.TeamNumber).ToArray();
        if (teams.Length != 1 || teams[0]?["user_unit_id_list"] is not JsonArray members || members.Count is < 1 or > 6)
            return new(409, null, "Team unavailable.");
        var ids = members.Select(value => value!.GetValue<long>()).ToArray();
        var positive = ids.Where(id => id > 0).ToArray();
        var owned = PlayerState.user["unit"]!["unit_list"]!.AsArray().Select(row => row!["user_unit_id"]!.GetValue<long>()).ToHashSet();
        if (positive.Length == 0 || ids.Any(id => id < 0) || positive.Distinct().Count() != positive.Length || positive.Any(id => !owned.Contains(id)))
            return new(409, null, "Invalid team members.");
        var pending = new JsonObject
        {
            ["tower_id"] = rule.TowerId,
            ["battle_id"] = rule.BattleId,
            ["floor"] = rule.Floor,
            ["team_number"] = rule.TeamNumber,
            ["unit_ids"] = members.DeepClone()
        };
        if (PlayerState.user["_local_tower_pending"] is JsonObject previous && !JsonNode.DeepEquals(previous, pending))
            return new(409, null, "Another floor entry is pending.");
        var updated = PlayerState.user.DeepClone().AsObject();
        updated["_local_tower_pending"] = pending;
        PlayerState.Save(updated);
        return new(0, new());
    }

    internal LocalReply EndVersus(LocalVersusEnd? rule)
    {
        if (rule is null || rule.Result is not (0 or 1) || rule.Id <= 0 || rule.BaseLevel is < 60 or > 100 || rule.FinalLevel < rule.BaseLevel || rule.Rules.Any(id => id <= 0) || rule.Rules.Distinct().Count() != rule.Rules.Length)
            return new(400, null, "Invalid challenge result.");
        var pending = PlayerState.user["_local_versus_pending"];
        if (pending?["id"]?.GetValue<int>() != rule.Id || pending?["base_level"]?.GetValue<int>() != rule.BaseLevel || pending?["final_level"]?.GetValue<int>() != rule.FinalLevel || pending?["unit_ids"] is not JsonArray members || pending?["rule_ids"] is not JsonArray rules || !rules.Select(id => id!.GetValue<int>()).Order().SequenceEqual(rule.Rules.Order()))
            return new(409, null, "Matching pending challenge required.");
        if (rule.Defeated.Distinct().Count() != rule.Defeated.Length || rule.Defeated.Any(id => id <= 0 || !members.Any(value => value!.GetValue<long>() == id)))
            return new(400, null, "Invalid defeated members.");
        if (rule.Ranking)
            return new(503, null, "Ranking registration unavailable.");
        var overview = VersusOverview([rule.Id]);
        if (overview.Status != 0)
            return overview;
        var progress = overview.Body!["clear_info"]![0]!.DeepClone();
        var baseLevel = progress["cleared_level"]!.GetValue<int>();
        var extraLevels = progress["add_rules_cleared_level"]!.AsArray().Select(level => level!.GetValue<int>()).ToList();
        var highest = Math.Max(baseLevel, extraLevels.DefaultIfEmpty(0).Max());
        var updated = PlayerState.user.DeepClone().AsObject();
        var drops = new JsonArray();
        if (rule.Result == 1)
        {
            if (rule.Rewards is null || rule.Rewards.Any(row => row.Level < 60) || rule.Rewards.Select(row => row.Level).Distinct().Count() != rule.Rewards.Length)
                return new(503, null, "Challenge rewards unavailable.");
            var current = rule.Rewards.SingleOrDefault(row => row.Level == rule.FinalLevel);
            if (current is null || current.HasDrops)
                return new(503, null, "Challenge drop settlement unavailable.");
            var firstLevel = Math.Max(60L, (long)highest + 1);
            var newlyCleared = rule.Rewards.Where(row => row.Level >= firstLevel && row.Level <= rule.FinalLevel).OrderBy(row => row.Level).ToArray();
            if (newlyCleared.LongLength != Math.Max(0L, (long)rule.FinalLevel - firstLevel + 1))
                return new(503, null, "Intermediate reward definitions unavailable.");
            foreach (var row in newlyCleared)
            {
                var reward = row.FirstReward;
                if (reward is null)
                    continue;
                var error = EconomyService.ApplyInventoryReward(updated, reward);
                if (error is not null)
                    return error;
                drops.Add(new JsonObject { ["object_type"] = reward.Type, ["object_id"] = reward.Id, ["object_value"] = reward.Count, ["rate_up_flg"] = 0, ["point_up_flg"] = 0 });
            }

            progress["cleared_level"] = Math.Max(baseLevel, rule.BaseLevel);
            if (rule.Rules.Length > 0 && rule.FinalLevel >= 100)
                extraLevels.Add(rule.FinalLevel);
            progress["add_rules_cleared_level"] = new JsonArray(extraLevels.Distinct().Order().Select(level => (JsonNode)JsonValue.Create(level)!).ToArray());
            var saved = updated["_local_versus_clear_info"] as JsonArray ?? new JsonArray();
            if (saved.Parent is null)
                updated["_local_versus_clear_info"] = saved;
            var previous = saved.FirstOrDefault(row => row?["multiple_spirits_weekly_master_id"]?.GetValue<int>() == rule.Id);
            if (previous != null)
                saved.Remove(previous);
            saved.Add(progress);
        }

        updated.Remove("_local_versus_pending");
        PlayerState.Save(updated);
        return new(0, new() { ["drop_list"] = drops, ["destiny_drop_list"] = new JsonArray(), ["user_update"] = new JsonObject { ["gold"] = updated["gold"]!.DeepClone(), ["crystal"] = updated["crystal"]!.DeepClone(), ["item"] = updated["item"]!.DeepClone() } });
    }

    internal LocalReply VersusOverview(long[]? ids)
    {
        if (ids is null || ids.Any(id => id <= 0) || ids.Distinct().Count() != ids.Length)
            return new(503, null, "Entry definitions unavailable.");
        var saved = PlayerState.user["_local_versus_clear_info"] as JsonArray ?? new JsonArray();
        var entries = new JsonArray();
        foreach (var id in ids)
        {
            var matches = saved.Where(row => row?["multiple_spirits_weekly_master_id"]?.GetValue<long>() == id).ToArray();
            if (matches.Length > 1)
                return new(409, null, "Duplicate clear progress.");
            var entry = matches.FirstOrDefault();
            if (entry != null && (entry["cleared_level"]?.GetValue<int>() is not (>= 0 and <= 100) || entry["add_rules_cleared_level"] is not JsonArray levels || levels.Any(level => level is null || level.GetValue<int>() < 100)))
                return new(409, null, "Invalid clear progress.");
            entries.Add(entry?.DeepClone() ?? new JsonObject { ["multiple_spirits_weekly_master_id"] = id, ["cleared_level"] = 0, ["add_rules_cleared_level"] = new JsonArray() });
        }

        return new(0, new() { ["clear_info"] = entries });
    }

    internal LocalReply StartVersus(LocalVersusStart? rule)
    {
        if (rule is null || rule.Id <= 0 || rule.ReleaseEpisode <= 0)
            return new(503, null, "Entry definition unavailable.");
        if (!rule.Active)
            return new(409, null, "Entry is inactive.");
        if (rule.Helper)
            return new(503, null, "Helper admission unavailable.");
        if (rule.BaseLevel is < 60 or > 100 || rule.TeamNumber <= 0 || rule.Rules.Any(row => row.Id <= 0 || row.AddedLevel < 0) || rule.Rules.Select(row => row.Id).Distinct().Count() != rule.Rules.Length || (long)rule.BaseLevel + rule.Rules.Sum(row => (long)row.AddedLevel) != rule.FinalLevel)
            return new(400, null, "Invalid challenge levels.");
        var released = (PlayerState.user["quest_playdata"] as JsonArray)?.Any(row => row?["episode_id"]?.GetValue<int>() == rule.ReleaseEpisode && row?["normal"]?["status"]?.GetValue<int>() >= 2) == true;
        if (!released)
            return new(409, null, "Entry is locked.");
        var overview = VersusOverview([rule.Id]);
        if (overview.Status != 0)
            return overview;
        var cleared = overview.Body!["clear_info"]![0]!["cleared_level"]!.GetValue<int>();
        if (rule.Rules.Length > 0 && cleared < 100)
            return new(409, null, "Additional rules are locked.");
        var cost = PlayerState.user["_local_versus_entry_costs"]?[rule.Id.ToString()]?.GetValue<int>();
        if (cost is null or < 0)
            return new(503, null, "Local entry cost unavailable.");
        var teams = PlayerState.user["team"]!["team_list"]!.AsArray().Where(row => row?["number"]?.GetValue<int>() == rule.TeamNumber).ToArray();
        if (teams.Length != 1 || teams[0]?["user_unit_id_list"] is not JsonArray members || members.Count is < 1 or > 6)
            return new(409, null, "Team unavailable.");
        var ids = members.Select(row => row!.GetValue<long>()).ToArray();
        var positive = ids.Where(id => id > 0).ToArray();
        var units = PlayerState.user["unit"]!["unit_list"]!.AsArray();
        if (positive.Length == 0 || ids.Any(id => id < 0) || positive.Distinct().Count() != positive.Length || positive.Any(id => units.Count(row => row?["user_unit_id"]?.GetValue<long>() == id) != 1))
            return new(409, null, "Invalid team members.");
        var pending = new JsonObject
        {
            ["id"] = rule.Id,
            ["base_level"] = rule.BaseLevel,
            ["final_level"] = rule.FinalLevel,
            ["team_number"] = rule.TeamNumber,
            ["unit_ids"] = members.DeepClone(),
            ["cost"] = cost.Value,
            ["rule_ids"] = new JsonArray(rule.Rules.OrderBy(row => row.Id).Select(row => (JsonNode)JsonValue.Create(row.Id)!).ToArray())
        };
        if (PlayerState.user["_local_versus_pending"] is JsonObject previous)
            return JsonNode.DeepEquals(previous, pending) ? new(0, new() { ["user_update"] = new JsonObject { ["phantasm_point"] = PlayerState.user["phantasm_point"]!.DeepClone() } }) : new(409, null, "Another entry is pending.");
        var points = PlayerState.user["phantasm_point"]?["points"]?.GetValue<int>();
        if (points is null or < 0 || points < cost)
            return new(409, null, "Insufficient entry points.");
        var updated = PlayerState.user.DeepClone().AsObject();
        updated["phantasm_point"]!["points"] = points.Value - cost.Value;
        updated["_local_versus_pending"] = pending;
        PlayerState.Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["phantasm_point"] = updated["phantasm_point"]!.DeepClone() } });
    }
}
