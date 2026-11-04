using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalTowerStart(int TowerId, int BattleId, int Floor, int TeamNumber);

public sealed partial class OfflineServer
{
    private LocalReply StartTower(LocalTowerStart? rule)
    {
        if (rule is null || rule.TowerId <= 0 || rule.BattleId <= 0 || rule.Floor <= 0 || rule.TeamNumber <= 0)
            return new(503, null, "Floor definition unavailable.");
        var cleared = user["tower"]?["cleared_floor"]?.GetValue<int>();
        if (cleared is null or < 0) return new(503, null, "Floor progress unavailable.");
        if (rule.Floor > (long)cleared + 1) return new(409, null, "Floor is not unlocked.");
        var snapshot = TowerUnits();
        if (snapshot.Status != 0) return snapshot;
        var teams = user["team"]!["team_list"]!.AsArray().Where(row => row?["number"]?.GetValue<int>() == rule.TeamNumber).ToArray();
        if (teams.Length != 1 || teams[0]?["user_unit_id_list"] is not JsonArray members || members.Count is < 1 or > 6)
            return new(409, null, "Team unavailable.");
        var ids = members.Select(value => value!.GetValue<long>()).ToArray();
        var positive = ids.Where(id => id > 0).ToArray();
        var owned = user["unit"]!["unit_list"]!.AsArray().Select(row => row!["user_unit_id"]!.GetValue<long>()).ToHashSet();
        if (positive.Length == 0 || ids.Any(id => id < 0) || positive.Distinct().Count() != positive.Length
            || positive.Any(id => !owned.Contains(id))) return new(409, null, "Invalid team members.");
        var pending = new JsonObject { ["tower_id"] = rule.TowerId, ["battle_id"] = rule.BattleId,
            ["floor"] = rule.Floor, ["team_number"] = rule.TeamNumber, ["unit_ids"] = members.DeepClone() };
        if (user["_local_tower_pending"] is JsonObject previous && !JsonNode.DeepEquals(previous, pending))
            return new(409, null, "Another floor entry is pending.");
        var updated = user.DeepClone().AsObject();
        updated["_local_tower_pending"] = pending;
        Save(updated);
        return new(0, new());
    }
}
