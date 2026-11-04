using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalRecycleGain(int Type, int Id, int Value);
public sealed record LocalPictureRecycleEntry(long Id, int PictureId, int Strength, int LimitBreak, LocalRecycleGain[] Gains);
public sealed record LocalPictureRecycle(LocalPictureRecycleEntry[] Entries, int GoldMax, int PointMax, int ItemMax);

public sealed partial class OfflineServer
{
    private LocalReply RecyclePictures(LocalArguments? request)
    {
        if (request?.Ids is not { Length: > 0 } || request.Ids.Any(id => id <= 0)
            || request.Ids.Distinct().Count() != request.Ids.Length) return new(400, null, "Invalid equipment selection.");
        var rule = request.PictureRecycle;
        if (rule?.Entries is null || rule.Entries.Length != request.Ids.Length
            || !rule.Entries.Select(entry => entry.Id).Order().SequenceEqual(request.Ids.Order()))
            return new(503, null, "Recycle definitions unavailable.");
        var updated = user.DeepClone().AsObject();
        var pictures = updated["picture"]!["picture_list"]!.AsArray();
        var consumed = request.Ids.ToHashSet();
        if (updated["_local_idle"]?["picture_slot_list"] is JsonArray slots
            && slots.Any(slot => consumed.Contains(slot?["user_object_id"]?.GetValue<long>() ?? 0)))
            return new(409, null, "Equipment is in use.");
        foreach (var entry in rule.Entries)
        {
            var picture = pictures.FirstOrDefault(value => value!["user_picture_id"]!.GetValue<long>() == entry.Id);
            if (picture is null) return new(404, null, "Owned equipment not found.");
            if (picture["is_protection"]?.GetValue<int>() != 0) return new(409, null, "Equipment is protected.");
            if (picture["picture_id"]!.GetValue<int>() != entry.PictureId || picture["spec_actualize"]!.GetValue<int>() != entry.Strength
                || picture["potential_limitbreak"]!.GetValue<int>() != entry.LimitBreak)
                return new(409, null, "Recycle definition is stale.");
            if (entry.Gains is null || entry.Gains.Any(gain => gain.Value < 0 || gain.Value > 0 && gain.Type is not (2 or 3 or 5 or 12)))
                return new(503, null, "Recycle reward type unsupported.");
        }
        var gains = new JsonArray();
        foreach (var group in rule.Entries.SelectMany(entry => entry.Gains).Where(gain => gain.Value > 0).GroupBy(gain => (gain.Type, gain.Id)))
        {
            var amount = group.Sum(gain => (long)gain.Value);
            if (amount > int.MaxValue) return new(409, null, "Recycle reward exceeds capacity.");
            if (group.Key.Type == 12)
            {
                if (group.Key.Id <= 0 || rule.ItemMax <= 0) return new(503, null, "Item definition unavailable.");
                var items = updated["item"]!["item_list"]!.AsArray();
                var item = items.FirstOrDefault(value => value!["item_id"]!.GetValue<int>() == group.Key.Id);
                var total = (item?["num"]?.GetValue<int>() ?? 0) + amount;
                if (total > rule.ItemMax) return new(409, null, "Item capacity exceeded.");
                if (item is null) items.Add(new JsonObject { ["item_id"] = group.Key.Id, ["num"] = (int)total });
                else item["num"] = (int)total;
            }
            else
            {
                var key = group.Key.Type == 3 ? "gold" : group.Key.Type == 2 ? "points" : "piece";
                var owner = group.Key.Type == 2 ? updated["phantasm_point"]!.AsObject() : updated;
                var maximum = group.Key.Type == 3 ? rule.GoldMax : group.Key.Type == 2 ? rule.PointMax : int.MaxValue;
                if (maximum <= 0 || group.Key.Id != 0) return new(503, null, "Currency definition unavailable.");
                var total = owner[key]!.GetValue<int>() + amount;
                if (total > maximum) return new(409, null, "Currency capacity exceeded.");
                owner[key] = (int)total;
            }
            gains.Add(new JsonObject { ["type"] = group.Key.Type, ["id"] = group.Key.Id, ["value"] = (int)amount });
        }
        foreach (var unit in updated["unit"]!["unit_list"]!.AsArray())
            for (var slot = 1; slot <= 5; slot++)
                if (consumed.Contains(unit![$"equip_spellcard{slot}_user_picture_id"]?.GetValue<long>() ?? 0))
                    unit[$"equip_spellcard{slot}_user_picture_id"] = 0L;
        foreach (var picture in pictures.Where(value => consumed.Contains(value!["user_picture_id"]!.GetValue<long>())).ToArray()) pictures.Remove(picture);
        Save(updated);
        return new(0, new()
        {
            ["gain_object_list"] = gains,
            ["remove_user_picture_id_list"] = new JsonArray(request.Ids.Select(id => (JsonNode?)JsonValue.Create(id)).ToArray()),
            ["user_update"] = new JsonObject
            {
                ["picture"] = updated["picture"]!.DeepClone(), ["unit"] = updated["unit"]!.DeepClone(),
                ["gold"] = updated["gold"]!.DeepClone(), ["phantasm_point"] = updated["phantasm_point"]!.DeepClone(),
                ["piece"] = updated["piece"]!.DeepClone(), ["item"] = updated["item"]!.DeepClone()
            }
        });
    }
}
