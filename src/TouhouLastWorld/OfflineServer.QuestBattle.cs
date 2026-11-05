using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalQuestBattleStart(int EpisodeId, int BattleId, int Difficulty, int TeamNumber,
    bool Open, int Cost, long[] Participants, long[] OwnedParticipants, LocalDailyQuestEntry? Daily = null);
public sealed record LocalBattleLevel(int Level, int Exp);
public sealed record LocalBattleUnitGrowth(long UnitId, LocalBattleLevel[] Levels);
public sealed record LocalBattleRewards(int GoldMin, int GoldMax, int GoldCapacity, int PlayerExp, int UnitExp,
    int[] DefeatLimits, LocalBattleLevel[] PlayerLevels, LocalBattleUnitGrowth[] Units);
public sealed record LocalQuestBattleEnd(int BattleId, int Result, long[] Defeated, LocalBattleRewards? Rewards,
    LocalClearScore? ClearScore = null);

public sealed partial class OfflineServer
{
    private const string PendingQuestBattle = "_local_quest_battle_pending";
    private const string ChargeQuestOnWin = "charge_on_win";
    private static readonly string[] QuestDifficulties = ["normal", "hard", "lunatic"];

    internal int PendingQuestBattleEpisode => user[PendingQuestBattle]?["episode_id"]?.GetValue<int>() ?? 0;
    internal int PendingQuestBattleDifficulty => user[PendingQuestBattle]?["difficulty"]?.GetValue<int>() ?? -1;
    internal long[] PendingQuestBattleOwnedUnits => (user[PendingQuestBattle]?["owned"] as JsonArray)?
        .Select(id => id!.GetValue<long>()).ToArray() ?? [];

    private LocalReply StartQuestBattle(LocalQuestBattleStart? request)
    {
        if (request is null) return new(501, null, "This battle entry is unavailable offline.");
        var daily = request.Daily;
        if ((daily is null ? request.EpisodeId <= 0 : request.EpisodeId != 0 || request.Difficulty != 0 || request.TeamNumber == 0)
            || request.BattleId <= 0 || request.Difficulty is < 0 or > 2
            || request.TeamNumber < 0
            || request.Cost < 0 || request.Participants.Length == 0
            || request.Participants.Any(id => id <= 0) || request.OwnedParticipants.Any(id => !request.Participants.Contains(id)))
            return new(400, null, "Invalid quest battle entry.");
        if (!request.Open) return new(409, null, "Quest battle is locked.");
        if (daily is not null && CheckDailyQuest(daily) is { } unavailable) return unavailable;
        if (request.TeamNumber == 0)
        {
            if (!request.OwnedParticipants.SequenceEqual(storyTeamMembers))
                return new(409, null, "Battle formation does not match the story team.");
        }
        else
        {
            var team = user["team"]!["team_list"]!.AsArray()
                .FirstOrDefault(row => row?["number"]?.GetValue<int>() == request.TeamNumber);
            if (team?["user_unit_id_list"] is not JsonArray slots
                || request.OwnedParticipants.Any(id => !slots.Any(slot => slot!.GetValue<long>() == id)))
                return new(409, null, "Battle formation does not match the saved team.");
        }

        var points = user["phantasm_point"]!["points"]!.GetValue<int>();
        if (points < request.Cost) return new(409, null, "Not enough spirit points.");
        var pending = user[PendingQuestBattle];
        if (pending?["episode_id"]?.GetValue<int>() == request.EpisodeId
            && pending["battle_id"]?.GetValue<int>() == request.BattleId
            && (daily is null || pending["daily_stage_id"]?.GetValue<int>() == daily.StageId
                && pending[DailyQuestDay]?.GetValue<int>() == user[DailyQuestDay]?.GetValue<int>())
            && pending["difficulty"]?.GetValue<int>() == request.Difficulty
            && pending["team_number"]?.GetValue<int>() == request.TeamNumber
            && pending["cost"]?.GetValue<int>() == request.Cost
            && pending["participants"]!.AsArray().Select(id => id!.GetValue<long>()).SequenceEqual(request.Participants))
            return QuestBattleAdmissionReply();

        var updated = user.DeepClone().AsObject();
        updated[PendingQuestBattle] = new JsonObject
        {
            ["episode_id"] = request.EpisodeId, ["battle_id"] = request.BattleId,
            ["difficulty"] = request.Difficulty, ["team_number"] = request.TeamNumber, ["cost"] = request.Cost,
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
        Save(updated);
        return QuestBattleAdmissionReply();
    }

    private LocalReply QuestBattleAdmissionReply() => new(0, new()
    {
        ["user_update"] = new JsonObject { ["phantasm_point"] = user["phantasm_point"]!.DeepClone() }
    });

    private LocalReply EndQuestBattle(LocalQuestBattleEnd? request)
    {
        if (request is null || request.BattleId <= 0 || request.Result is not (0 or 1))
            return new(400, null, "Invalid quest battle result.");
        var pending = user[PendingQuestBattle];
        var episodeId = PendingQuestBattleEpisode;
        var difficulty = PendingQuestBattleDifficulty;
        var isDaily = PendingDailyQuestStage > 0;
        if ((!isDaily && episodeId <= 0) || difficulty is < 0 or > 2 || pending?["battle_id"]?.GetValue<int>() != request.BattleId)
            return new(409, null, "Matching quest battle entry required.");
        var participants = pending["participants"]!.AsArray().Select(id => id!.GetValue<long>()).ToHashSet();
        if (request.Defeated.Distinct().Count() != request.Defeated.Length
            || request.Defeated.Any(id => !participants.Contains(id)))
            return new(400, null, "Invalid defeated battle members.");

        var won = request.Result == 1;
        var rewards = request.Rewards;
        if (won && (rewards is null || rewards.GoldMin < 0 || rewards.GoldMax < rewards.GoldMin
            || rewards.GoldCapacity <= 0 || rewards.PlayerExp < 0 || rewards.UnitExp < 0
            || rewards.PlayerLevels.Length == 0 || rewards.Units.Any(unit => unit.Levels.Length == 0)
            || !rewards.Units.Select(unit => unit.UnitId).SequenceEqual(PendingQuestBattleOwnedUnits)))
            return new(503, null, "Quest battle reward definitions unavailable.");

        var updated = user.DeepClone().AsObject();
        var challenges = new JsonArray();
        var drops = new JsonArray();
        var changes = new JsonObject();
        if (won && rewards is not null)
        {
            var points = updated["phantasm_point"]!["points"]!.GetValue<int>();
            var cost = pending["cost"]!.GetValue<int>();
            if (points < cost) return new(409, null, "Not enough spirit points.");
            updated["phantasm_point"]!["points"] = points - cost;
            changes["phantasm_point"] = updated["phantasm_point"]!.DeepClone();
            if (!isDaily && request.ClearScore is { } score && participants.SetEquals(PendingQuestBattleOwnedUnits))
                RecordQuestClear(updated, request.BattleId, PendingQuestBattleOwnedUnits, score);
            updated[QuestWinCount] = (int)Math.Min((long)QuestWins() + 1, int.MaxValue);
            for (var index = 0; index < rewards.DefeatLimits.Length; index++)
                if (request.Defeated.Length <= rewards.DefeatLimits[index]) challenges.Add(index + 1);
            var gold = updated["gold"]!.GetValue<int>();
            var rolledGold = (int)Random.Shared.NextInt64(rewards.GoldMin, (long)rewards.GoldMax + 1);
            var earned = Math.Min(rolledGold, Math.Max(0, rewards.GoldCapacity - gold));
            updated["gold"] = gold + earned;
            if (earned > 0)
                drops.Add(new JsonObject { ["object_type"] = 3, ["object_id"] = 0, ["object_value"] = earned });
            AddBattleExperience(updated, rewards.PlayerExp, rewards.PlayerLevels);
            var units = updated["unit"]!["unit_list"]!.AsArray();
            foreach (var growth in rewards.Units)
            {
                var unit = units.FirstOrDefault(row => row?["user_unit_id"]?.GetValue<long>() == growth.UnitId);
                if (unit is null) return new(409, null, "Battle participant is no longer owned.");
                AddBattleExperience(unit.AsObject(), rewards.UnitExp, growth.Levels);
            }
            foreach (var key in new[] { "gold", "exp", "level", "unit" })
                changes[key] = updated[key]!.DeepClone();
        }
        var resultState = won ? (challenges.Count == 3 ? 3 : 2) : 1;
        if (isDaily)
            UpdateDailyQuestProgress(updated, pending.AsObject(), resultState, challenges, won, changes);
        else
            UpdateStoryBattleProgress(updated, episodeId, difficulty, resultState, challenges, changes);
        updated.Remove(PendingQuestBattle);
        Save(updated);
        return new(0, new()
        {
            ["drop_list"] = drops, ["destiny_drop_list"] = new JsonArray(),
            ["battle_challenge_result"] = challenges.Count, ["user_update"] = changes
        });
    }

    private static void UpdateStoryBattleProgress(JsonObject updated, int episodeId, int difficulty, int state,
        JsonArray challenges, JsonObject changes)
    {
        var episodes = updated["quest_playdata"] as JsonArray ?? new JsonArray();
        var episode = episodes.FirstOrDefault(row => row?["episode_id"]?.GetValue<int>() == episodeId) as JsonObject;
        if (episode is null)
        {
            episode = new JsonObject { ["episode_id"] = episodeId, ["scenario_status"] = 0 };
            episodes.Add(episode);
        }
        foreach (var key in QuestDifficulties)
            episode[key] ??= new JsonObject { ["status"] = -2, ["battle_challenge"] = new JsonArray() };
        SetQuestProgress(episode[QuestDifficulties[difficulty]]!, state, challenges);
        if (episodes.Parent is null) updated["quest_playdata"] = episodes;
        changes["quest_playdata"] = episodes.DeepClone();
    }

    private static void SetQuestProgress(JsonNode entry, int state, JsonArray challenges)
    {
        var previous = entry["battle_challenge"] as JsonArray;
        if (previous is null || challenges.Count > previous.Count) entry["battle_challenge"] = challenges.DeepClone();
        entry["status"] = Math.Max(entry["status"]?.GetValue<int>() ?? -2, state);
    }

    private static bool RefundInterruptedQuest(JsonObject updated)
    {
        if (updated[PendingQuestBattle] is not JsonObject pending || pending[ChargeQuestOnWin]?.GetValue<bool>() == true
            || pending["cost"]?.GetValue<int>() is not >= 0)
            return false;
        var balance = updated["phantasm_point"]!;
        balance["points"] = (int)Math.Min(int.MaxValue,
            (long)balance["points"]!.GetValue<int>() + pending["cost"]!.GetValue<int>());
        updated.Remove(PendingQuestBattle);
        return true;
    }

    private static void AddBattleExperience(JsonObject entity, int amount, LocalBattleLevel[] levels)
    {
        var exp = entity["exp"]!.GetValue<long>();
        var next = exp + Math.Min(amount, Math.Max(0L, levels.Max(row => row.Exp) - exp));
        entity["exp"] = next;
        var level = entity["level"]!.GetValue<int>();
        entity["level"] = levels.Where(row => row.Exp <= next).Select(row => row.Level).Append(level).Max();
    }
}
