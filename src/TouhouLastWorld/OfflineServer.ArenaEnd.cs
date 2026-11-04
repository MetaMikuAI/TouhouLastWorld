using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalArenaScore(int AttackCount, int GrazeCount, int Total, int Attack, int Graze, int Defeated);
public sealed record LocalArenaReward(int Id, int Score, int Value);
public sealed record LocalArenaEnd(int Result, int BattleNumber, long[] Defeated, bool ExtraTeams,
    LocalArenaScore? Score, LocalArenaReward[]? Rewards, int Capacity);

public sealed partial class OfflineServer
{
    private LocalReply EndArena(LocalArenaEnd? rule)
    {
        if (rule is null || rule.Result is not (0 or 1) || rule.ExtraTeams)
            return new(400, null, "Invalid battle result.");
        if (user["_local_arena_pending"] is not JsonObject pending
            || pending["battle_number"]?.GetValue<int>() != rule.BattleNumber
            || pending["unit_ids"] is not JsonArray members)
            return new(409, null, "Matching battle entry unavailable.");
        var ids = members.Select(row => row!.GetValue<long>()).Where(id => id > 0).ToArray();
        if (rule.Defeated.Any(id => id <= 0 || !ids.Contains(id)) || rule.Defeated.Distinct().Count() != rule.Defeated.Length)
            return new(400, null, "Invalid defeated members.");
        var category = pending["category"]?.GetValue<int>() ?? 0;
        if (category is not (1 or 2)) return new(409, null, "Invalid pending category.");
        if (rule.Result == 1 && category == 1) return new(503, null, "Daily reward settlement unavailable.");
        var updated = user.DeepClone().AsObject();
        var rewards = new JsonArray();
        if (rule.Result == 1)
        {
            if (rule.BattleNumber is < 1 or > 4 || rule.Score is not { AttackCount: >= 0, GrazeCount: >= 0 } score
                || rule.Rewards is null || rule.Capacity <= 0
                || rule.Rewards.Any(row => row.Id <= 0 || row.Score < 0 || row.Value < 0)
                || rule.Rewards.Select(row => row.Id).Distinct().Count() != rule.Rewards.Length)
                return new(503, null, "Weekly score or reward definitions unavailable.");
            var difficulty = pending["difficulty"]!.GetValue<int>();
            var attack = score.AttackCount is >= 1 and <= 5 ? 24 : score.AttackCount is >= 6 and <= 16 ? 34 - 2 * score.AttackCount : 0;
            var graze = (int)Math.Max(0, 20L - 2L * score.GrazeCount);
            var defeated = Math.Max(0, 20 - 10 * rule.Defeated.Length);
            var total = attack + graze + defeated + (difficulty == 3 ? 15 : difficulty == 4 ? 36 : 0);
            if (score.Total != total || score.Attack != attack || score.Graze != graze || score.Defeated != defeated)
                return new(409, null, "Score does not match battle counters.");
            var overview = updated["_local_weekly_arena"];
            if (overview?["custom_battle_id"]?.GetValue<int>() != pending["custom_battle_id"]!.GetValue<int>()
                || overview?["weekly_battle_data"] is not JsonObject battles
                || overview["total_battle_score"]?.GetValue<int>() is not >= 0)
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
            if (combined > int.MaxValue) return new(409, null, "Invalid combined score.");
            var best = Math.Max(previous, (int)combined);
            var grant = rule.Rewards.Where(row => row.Score > previous && row.Score <= best).Sum(row => (long)row.Value);
            var balance = updated["arena_point"]?.GetValue<int>();
            if (balance is null or < 0 || balance + grant > rule.Capacity)
                return new(409, null, "Reward exceeds point capacity.");
            updated["arena_point"] = balance.Value + (int)grant;
            overview["total_battle_score"] = best;
            if (grant > 0) rewards.Add(new JsonObject { ["object_type"] = 21, ["object_id"] = 0,
                ["object_value"] = (int)grant, ["rate_up_flg"] = 0, ["point_up_flg"] = 0 });
        }
        updated.Remove("_local_arena_pending");
        Save(updated);
        return new(0, new() { ["user_arena"] = new JsonObject {
                ["daily_cleared_difficulty"] = updated["_local_daily_arena"]?["clear_diffculty"]?.DeepClone() ?? JsonValue.Create(0) },
            ["first_cleared_reward_list"] = rewards,
            ["user_update"] = new JsonObject { ["arena_point"] = updated["arena_point"]!.DeepClone() } });
    }
}
