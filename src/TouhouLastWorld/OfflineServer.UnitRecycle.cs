using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalUnitRecycleEntry(long Id, int UnitId, int Rare, int LimitBreak, LocalRecycleGain[] Gains);
public sealed record LocalUnitRecycle(LocalUnitRecycleEntry[] Entries, int GoldMax, int PointMax, int ItemMax);

public sealed partial class OfflineServer
{
    private LocalReply RecycleUnits(LocalArguments? request)
    {
        if (request?.Ids is not { Length: > 0 and <= 10 } ids || ids.Any(id => id <= 0)
            || ids.Distinct().Count() != ids.Length) return new(400, null, "Invalid unit selection.");
        var rule = request.UnitRecycle;
        if (rule is null || !rule.Entries.Select(row => row.Id).Order().SequenceEqual(ids.Order()))
            return new(503, null, "Unit recycle definitions unavailable.");
        var updated = user.DeepClone().AsObject();
        var units = updated["unit"]!["unit_list"]!.AsArray();
        var selected = ids.ToHashSet();
        if (units.Count <= ids.Length) return new(409, null, "At least one owned unit required.");
        foreach (var row in rule.Entries)
        {
            var unit = units.FirstOrDefault(entry => entry!["user_unit_id"]!.GetValue<long>() == row.Id);
            if (unit is null) return new(404, null, "Owned unit not found.");
            if (unit["is_protection"]?.GetValue<int>() != 0 || unit["is_lecture"]?.GetValue<int>() != 0)
                return new(409, null, "Unit is protected or occupied.");
            if (unit["unit_id"]!.GetValue<int>() != row.UnitId || unit["rare"]!.GetValue<int>() != row.Rare
                || unit["level_limitbreak"]!.GetValue<int>() != row.LimitBreak)
                return new(409, null, "Unit recycle definition is stale.");
            if (row.Gains.Any(gain => gain.Value < 0 || gain.Value > 0 && gain.Type is not (2 or 3 or 5 or 12)))
                return new(503, null, "Unit recycle reward unsupported.");
        }
        foreach (var section in new[] { "team", "home_unit_info", "favorite", "dispatch", "lecture_room", "training_room" })
            if (ContainsUnitReference(updated[section], selected)) return new(409, null, "Unit is assigned.");
        if (selected.Contains(updated["helper_user_unit_id1"]?.GetValue<long>() ?? 0)
            || ContainsUnitReference(updated["unit"]?["tower_unit_list"], selected)
            || updated["weekly_arena_user_unit_list"] is JsonArray weekly && weekly.Any(id => id is not null && selected.Contains(id.GetValue<long>()))
            || updated["_local_idle"]?["unit_slot_list"] is JsonArray slots
                && slots.Any(slot => selected.Contains(slot?["user_object_id"]?.GetValue<long>() ?? 0)))
            return new(409, null, "Unit is assigned.");
        var gains = new JsonArray();
        foreach (var group in rule.Entries.SelectMany(row => row.Gains).Where(gain => gain.Value > 0).GroupBy(gain => (gain.Type, gain.Id)))
        {
            var amount = group.Sum(gain => (long)gain.Value);
            if (amount > int.MaxValue) return new(409, null, "Recycle reward exceeds capacity.");
            if (group.Key.Type is 3 or 12)
            {
                var error = ApplyInventoryReward(updated, new(group.Key.Type, group.Key.Id, (int)amount,
                    group.Key.Type == 3 ? rule.GoldMax : rule.ItemMax));
                if (error is not null) return error;
            }
            else
            {
                if (group.Key.Id != 0) return new(503, null, "Currency definition unavailable.");
                var points = group.Key.Type == 2;
                var owner = points ? updated["phantasm_point"]!.AsObject() : updated;
                var key = points ? "points" : "piece";
                var maximum = points ? rule.PointMax : int.MaxValue;
                var total = owner[key]!.GetValue<int>() + amount;
                if (maximum <= 0 || total > maximum) return new(409, null, "Currency capacity exceeded.");
                owner[key] = (int)total;
            }
            gains.Add(new JsonObject { ["type"] = group.Key.Type, ["id"] = group.Key.Id, ["value"] = (int)amount });
        }
        updated["_local_unit_sequence"] = Math.Max(updated["_local_unit_sequence"]?.GetValue<long>() ?? 0,
            units.Select(entry => entry!["user_unit_id"]!.GetValue<long>()).Max());
        foreach (var picture in updated["picture"]!["picture_list"]!.AsArray())
            if (selected.Contains(picture!["equip_user_unit_id"]?.GetValue<long>() ?? 0)) picture["equip_user_unit_id"] = 0L;
        foreach (var unit in units.Where(entry => selected.Contains(entry!["user_unit_id"]!.GetValue<long>())).ToArray()) units.Remove(unit);
        Save(updated);
        var changes = new JsonObject();
        foreach (var key in new[] { "unit", "picture", "gold", "phantasm_point", "piece", "item" }) changes[key] = updated[key]!.DeepClone();
        return new(0, new() { ["gain_object_list"] = gains, ["user_update"] = changes,
            ["remove_user_unit_id_list"] = new JsonArray(ids.Select(id => (JsonNode?)JsonValue.Create(id)).ToArray()) });
    }

    private static bool ContainsUnitReference(JsonNode? node, HashSet<long> selected)
    {
        if (node is JsonArray list) return list.Any(entry => ContainsUnitReference(entry, selected));
        if (node is not JsonObject map) return false;
        foreach (var pair in map)
        {
            if (pair.Key == "user_unit_id" && pair.Value is JsonValue value && value.TryGetValue<long>(out var id) && selected.Contains(id)) return true;
            if (pair.Key is "user_unit_id_list" or "season_enhancement_user_unit_id_list" && pair.Value is JsonArray ids
                && ids.Any(id => id is not null && selected.Contains(id.GetValue<long>()))) return true;
            if (ContainsUnitReference(pair.Value, selected)) return true;
        }
        return false;
    }
}
