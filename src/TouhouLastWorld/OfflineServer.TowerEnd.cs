using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalTowerEnd(int TowerId, int BattleId, int Floor, int MaxFloor, int Result,
    string? Units, string? Enemies, long[]? Defeated, int[] EnemyIds,
    LocalInventoryReward[]? FirstRewards, LocalInventoryReward[]? RepeatRewards);

public sealed partial class OfflineServer
{
    public int PendingTowerId => user["_local_tower_pending"]?["tower_id"]?.GetValue<int>() ?? 0;

    private LocalReply EndTower(LocalTowerEnd? rule)
    {
        if (rule is null || rule.TowerId <= 0 || rule.BattleId <= 0 || rule.Floor <= 0 || rule.MaxFloor < rule.Floor
            || rule.Result is not (0 or 1) || rule.Units is null || rule.Enemies is null || rule.Defeated is null)
            return new(400, null, "Invalid floor result.");
        var pending = user["_local_tower_pending"];
        if (pending?["tower_id"]?.GetValue<int>() != rule.TowerId || pending?["battle_id"]?.GetValue<int>() != rule.BattleId
            || pending?["floor"]?.GetValue<int>() != rule.Floor || pending?["unit_ids"] is not JsonArray members)
            return new(409, null, "Matching pending floor entry required.");
        var cleared = user["tower"]!["cleared_floor"]!.GetValue<int>();
        var repeat = cleared == rule.MaxFloor && rule.Floor == cleared;
        if (!repeat && rule.Floor != (long)cleared + 1) return new(409, null, "Floor progress changed.");
        if (rule.Defeated.Distinct().Count() != rule.Defeated.Length
            || rule.Defeated.Any(id => id <= 0 || !members.Any(value => value!.GetValue<long>() == id)))
            return new(400, null, "Invalid defeated members.");
        var snapshot = TowerUnits(JsonValue.Create(rule.Units));
        if (snapshot.Status != 0) return snapshot;
        var snapshotIds = rule.Units.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => long.Parse(line.Split(',')[0])).ToHashSet();
        if (members.Any(value => value!.GetValue<long>() > 0 && !snapshotIds.Contains(value.GetValue<long>())))
            return new(400, null, "Participant snapshot missing.");
        foreach (var line in rule.Enemies.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = line.TrimEnd('\r').Split(',');
            if (fields.Length != 26 || fields.Any(field => !int.TryParse(field, out _))
                || !rule.EnemyIds.Contains(int.Parse(fields[0]))) return new(400, null, "Invalid enemy snapshot.");
        }
        if (rule.Result == 1 && rule.Enemies.Length != 0) return new(400, null, "Cleared floor must not retain enemies.");
        var updated = user.DeepClone().AsObject();
        var drops = new JsonArray();
        if (rule.Result == 1)
        {
            var rewards = repeat ? rule.RepeatRewards : rule.FirstRewards;
            if (rewards is null) return new(503, null, "Floor reward definition unavailable.");
            foreach (var reward in rewards)
            {
                var error = ApplyInventoryReward(updated, reward);
                if (error is not null) return error;
                drops.Add(new JsonObject { ["object_type"] = reward.Type, ["object_id"] = reward.Id,
                    ["object_value"] = reward.Count, ["rate_up_flg"] = 0, ["point_up_flg"] = 0 });
            }
            updated["tower"]!["cleared_floor"] = Math.Max(cleared, rule.Floor);
        }
        updated["_local_tower_unit_info"] = rule.Units;
        updated["_local_tower_enemy_info"] = rule.Enemies;
        updated.Remove("_local_tower_pending");
        Save(updated);
        return new(0, new() { ["drop_list"] = drops, ["destiny_drop_list"] = new JsonArray(), ["battle_challenge_result"] = 0,
            ["user_update"] = new JsonObject { ["tower"] = updated["tower"]!.DeepClone(), ["gold"] = updated["gold"]!.DeepClone(),
                ["crystal"] = updated["crystal"]!.DeepClone(), ["item"] = updated["item"]!.DeepClone() } });
    }
}
