using System.Text.Json.Nodes;
using System.Text;
using System.Text.Json;
using System.Globalization;
using System.Security.Cryptography;

namespace TouhouLastWorld;

internal sealed class PicturesService
{
    internal PicturesService(PlayerState playerState)
    {
        PlayerState = playerState;
    }

    private readonly PlayerState PlayerState;
    internal LocalReply SetCostumes(LocalCostumeEntry[]? changes)
    {
        if (changes is null || changes.Any(entry => entry.UserUnitId <= 0 || entry.CostumeId <= 0) || changes.Select(entry => entry.UserUnitId).Distinct().Count() != changes.Length)
            return new(400, null, "Invalid costume assignments.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var owned = updated["costume"]!["costume_id_list"]!.AsArray().Select(id => id!.GetValue<int>()).ToHashSet();
        var units = updated["unit"]!["unit_list"]!.AsArray();
        foreach (var change in changes)
        {
            if (change.UnitId <= 0)
                return new(503, null, "Costume definition unavailable.");
            var unit = units.FirstOrDefault(entry => entry!["user_unit_id"]!.GetValue<long>() == change.UserUnitId);
            if (unit is null)
                return new(404, null, "Owned unit not found.");
            if (unit["unit_id"]!.GetValue<int>() != change.UnitId)
                return new(400, null, "Costume does not match unit.");
            if (!change.IsDefault && !owned.Contains(change.CostumeId) && unit["equip_costume_id"]!.GetValue<int>() != change.CostumeId)
                return new(403, null, "Costume not owned.");
            unit["equip_costume_id"] = change.CostumeId;
        }

        PlayerState.Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["unit"] = updated["unit"]!.DeepClone() } });
    }

    internal LocalReply SetEquipment(SetEquipmentCommand? request)
    {
        if (request?.EntityId is null or <= 0 || request.Equipment is null || request.Equipment.Length > 5 || request.Equipment.Any(entry => entry.Slot is < 1 or > 5 || entry.PictureId < 0) || request.Equipment.Select(entry => entry.Slot).Distinct().Count() != request.Equipment.Length)
            return new(400, null, "Invalid equipment slots.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var units = updated["unit"]!["unit_list"]!.AsArray();
        var unit = units.FirstOrDefault(entry => entry!["user_unit_id"]!.GetValue<long>() == request.EntityId);
        if (unit is null)
            return new(404, null, "Owned unit not found.");
        var pictures = updated["picture"]!["picture_list"]!.AsArray();
        foreach (var change in request.Equipment)
        {
            if (change.PictureId > 0)
            {
                var picture = pictures.FirstOrDefault(entry => entry!["user_picture_id"]!.GetValue<long>() == change.PictureId);
                if (picture is null)
                    return new(404, null, "Owned equipment not found.");
                var owner = picture["equip_user_unit_id"]?.GetValue<long>() ?? 0;
                if ((owner != 0 && owner != request.EntityId) || units.Any(other => other != unit && Enumerable.Range(1, 5).Any(slot => other![$"equip_spellcard{slot}_user_picture_id"]?.GetValue<long>() == change.PictureId)))
                    return new(409, null, "Equipment must be released from its current owner.");
            }

            unit[$"equip_spellcard{change.Slot}_user_picture_id"] = change.PictureId;
        }

        var equipped = Enumerable.Range(1, 5).Select(slot => unit[$"equip_spellcard{slot}_user_picture_id"]!.GetValue<long>()).Where(id => id > 0).ToArray();
        if (equipped.Distinct().Count() != equipped.Length)
            return new(409, null, "Equipment cannot occupy multiple slots.");
        foreach (var picture in pictures)
        {
            if (equipped.Contains(picture!["user_picture_id"]!.GetValue<long>()))
                picture["equip_user_unit_id"] = request.EntityId.Value;
            else if (picture["equip_user_unit_id"]?.GetValue<long>() == request.EntityId)
                picture["equip_user_unit_id"] = 0L;
        }

        PlayerState.Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["unit"] = updated["unit"]!.DeepClone(), ["picture"] = updated["picture"]!.DeepClone() } });
    }

    internal LocalReply ResetEquipment(long? id)
    {
        if (id is null or <= 0)
            return new(400, null, "Positive unit ID required.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var unit = updated["unit"]!["unit_list"]!.AsArray().FirstOrDefault(entry => entry!["user_unit_id"]!.GetValue<long>() == id);
        if (unit is null)
            return new(404, null, "Owned unit not found.");
        for (var slot = 1; slot <= 5; slot++)
            unit[$"equip_spellcard{slot}_user_picture_id"] = 0L;
        foreach (var picture in updated["picture"]!["picture_list"]!.AsArray())
            if (picture!["equip_user_unit_id"]?.GetValue<long>() == id)
                picture["equip_user_unit_id"] = 0L;
        PlayerState.Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["unit"] = updated["unit"]!.DeepClone(), ["picture"] = updated["picture"]!.DeepClone() } });
    }

    internal LocalReply StrengthenPicture(StrengthenPictureCommand? request)
    {
        if (request?.EntityId is null or <= 0)
            return new(400, null, "Invalid equipment instance.");
        var rule = request.PictureGrowth;
        if (rule is null || rule.PictureId <= 0 || rule.Current < 0 || rule.Current == int.MaxValue || rule.Next != rule.Current + 1 || rule.Next > rule.Maximum || rule.ItemId <= 0 || rule.ItemCount <= 0 || rule.GoldCost < 0)
            return new(503, null, "Equipment growth definition unavailable.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var picture = updated["picture"]!["picture_list"]!.AsArray().FirstOrDefault(entry => entry!["user_picture_id"]!.GetValue<long>() == request.EntityId);
        if (picture is null)
            return new(404, null, "Owned equipment not found.");
        if (picture["picture_id"]!.GetValue<int>() != rule.PictureId || picture["spec_actualize"]!.GetValue<int>() != rule.Current)
            return new(409, null, "Equipment growth definition is stale.");
        var item = updated["item"]!["item_list"]!.AsArray().FirstOrDefault(entry => entry!["item_id"]!.GetValue<int>() == rule.ItemId);
        var gold = updated["gold"]!.GetValue<int>();
        if (item is null || item["num"]!.GetValue<int>() < rule.ItemCount || gold < rule.GoldCost)
            return new(409, null, "Insufficient equipment growth materials.");
        item["num"] = item["num"]!.GetValue<int>() - rule.ItemCount;
        updated["gold"] = gold - rule.GoldCost;
        picture["spec_actualize"] = rule.Next;
        PlayerState.Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["picture"] = updated["picture"]!.DeepClone(), ["item"] = updated["item"]!.DeepClone(), ["gold"] = gold - rule.GoldCost } });
    }

    internal LocalReply LimitBreakPicture(LimitBreakPictureCommand? request)
    {
        if (request?.EntityId is null or <= 0 || request.Ids is not { Length: > 0 } || request.Ids.Any(id => id <= 0 || id == request.EntityId) || request.Ids.Distinct().Count() != request.Ids.Length)
            return new(400, null, "Invalid equipment materials.");
        var rule = request.PictureLimitBreak;
        if (rule is null || rule.PictureId <= 0 || rule.Current < 0 || rule.Maximum < rule.Current || rule.GoldCosts is null || rule.GoldCosts.Length != request.Ids.Length || rule.GoldCosts.Any(cost => cost < 0))
            return new(503, null, "Equipment limit definition unavailable.");
        if ((long)rule.Current + request.Ids.Length > rule.Maximum)
            return new(409, null, "Equipment limit exceeded.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var pictures = updated["picture"]!["picture_list"]!.AsArray();
        var target = pictures.FirstOrDefault(entry => entry!["user_picture_id"]!.GetValue<long>() == request.EntityId);
        if (target is null)
            return new(404, null, "Owned equipment not found.");
        if (target["picture_id"]!.GetValue<int>() != rule.PictureId || target["potential_limitbreak"]!.GetValue<int>() != rule.Current)
            return new(409, null, "Equipment limit definition is stale.");
        var consumed = request.Ids.ToHashSet();
        if (updated["_local_idle"]?["picture_slot_list"] is JsonArray slots && slots.Any(slot => consumed.Contains(slot?["user_object_id"]?.GetValue<long>() ?? 0) || (slot?["user_object_id"]?.GetValue<long>() == request.EntityId && slot["content"]?.GetValue<int>() == 3)))
            return new(409, null, "Equipment growth is already in progress.");
        foreach (var id in consumed)
        {
            var material = pictures.FirstOrDefault(entry => entry!["user_picture_id"]!.GetValue<long>() == id);
            if (material is null)
                return new(404, null, "Owned material not found.");
            if (material["picture_id"]!.GetValue<int>() != rule.PictureId || material["is_protection"]?.GetValue<int>() != 0)
                return new(409, null, "Material type or protection prevents consumption.");
        }

        var cost = rule.GoldCosts.Sum(value => (long)value);
        var gold = updated["gold"]!.GetValue<int>();
        if (gold < cost)
            return new(409, null, "Insufficient gold.");
        foreach (var unit in updated["unit"]!["unit_list"]!.AsArray())
            for (var slot = 1; slot <= 5; slot++)
                if (consumed.Contains(unit![$"equip_spellcard{slot}_user_picture_id"]?.GetValue<long>() ?? 0))
                    unit[$"equip_spellcard{slot}_user_picture_id"] = 0L;
        foreach (var material in pictures.Where(entry => consumed.Contains(entry!["user_picture_id"]!.GetValue<long>())).ToArray())
            pictures.Remove(material);
        target["potential_limitbreak"] = rule.Current + request.Ids.Length;
        updated["gold"] = gold - (int)cost;
        PlayerState.Save(updated);
        return new(0, new() { ["remove_user_picture_id_list"] = new JsonArray(request.Ids.Select(id => (JsonNode?)JsonValue.Create(id)).ToArray()), ["user_update"] = new JsonObject { ["picture"] = updated["picture"]!.DeepClone(), ["unit"] = updated["unit"]!.DeepClone(), ["gold"] = gold - (int)cost } });
    }

    internal LocalReply RecyclePictures(RecyclePicturesCommand? request)
    {
        if (request?.Ids is not { Length: > 0 } || request.Ids.Any(id => id <= 0) || request.Ids.Distinct().Count() != request.Ids.Length)
            return new(400, null, "Invalid equipment selection.");
        var rule = request.PictureRecycle;
        if (rule?.Entries is null || rule.Entries.Length != request.Ids.Length || !rule.Entries.Select(entry => entry.Id).Order().SequenceEqual(request.Ids.Order()))
            return new(503, null, "Recycle definitions unavailable.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var pictures = updated["picture"]!["picture_list"]!.AsArray();
        var consumed = request.Ids.ToHashSet();
        if (updated["_local_idle"]?["picture_slot_list"] is JsonArray slots && slots.Any(slot => consumed.Contains(slot?["user_object_id"]?.GetValue<long>() ?? 0)))
            return new(409, null, "Equipment is in use.");
        foreach (var entry in rule.Entries)
        {
            var picture = pictures.FirstOrDefault(value => value!["user_picture_id"]!.GetValue<long>() == entry.Id);
            if (picture is null)
                return new(404, null, "Owned equipment not found.");
            if (picture["is_protection"]?.GetValue<int>() != 0)
                return new(409, null, "Equipment is protected.");
            if (picture["picture_id"]!.GetValue<int>() != entry.PictureId || picture["spec_actualize"]!.GetValue<int>() != entry.Strength || picture["potential_limitbreak"]!.GetValue<int>() != entry.LimitBreak)
                return new(409, null, "Recycle definition is stale.");
            if (entry.Gains is null || entry.Gains.Any(gain => gain.Value < 0 || gain.Value > 0 && gain.Type is not (2 or 3 or 5 or 12)))
                return new(503, null, "Recycle reward type unsupported.");
        }

        var gains = new JsonArray();
        foreach (var group in rule.Entries.SelectMany(entry => entry.Gains).Where(gain => gain.Value > 0).GroupBy(gain => (gain.Type, gain.Id)))
        {
            var amount = group.Sum(gain => (long)gain.Value);
            if (amount > int.MaxValue)
                return new(409, null, "Recycle reward exceeds capacity.");
            if (group.Key.Type == 12)
            {
                if (group.Key.Id <= 0 || rule.ItemMax <= 0)
                    return new(503, null, "Item definition unavailable.");
                var items = updated["item"]!["item_list"]!.AsArray();
                var item = items.FirstOrDefault(value => value!["item_id"]!.GetValue<int>() == group.Key.Id);
                var total = (item?["num"]?.GetValue<int>() ?? 0) + amount;
                if (total > rule.ItemMax)
                    return new(409, null, "Item capacity exceeded.");
                if (item is null)
                    items.Add(new JsonObject { ["item_id"] = group.Key.Id, ["num"] = (int)total });
                else
                    item["num"] = (int)total;
            }
            else
            {
                var key = group.Key.Type == 3 ? "gold" : group.Key.Type == 2 ? "points" : "piece";
                var owner = group.Key.Type == 2 ? updated["phantasm_point"]!.AsObject() : updated;
                var maximum = group.Key.Type == 3 ? rule.GoldMax : group.Key.Type == 2 ? rule.PointMax : int.MaxValue;
                if (maximum <= 0 || group.Key.Id != 0)
                    return new(503, null, "Currency definition unavailable.");
                var total = owner[key]!.GetValue<int>() + amount;
                if (total > maximum)
                    return new(409, null, "Currency capacity exceeded.");
                owner[key] = (int)total;
            }

            gains.Add(new JsonObject { ["type"] = group.Key.Type, ["id"] = group.Key.Id, ["value"] = (int)amount });
        }

        foreach (var unit in updated["unit"]!["unit_list"]!.AsArray())
            for (var slot = 1; slot <= 5; slot++)
                if (consumed.Contains(unit![$"equip_spellcard{slot}_user_picture_id"]?.GetValue<long>() ?? 0))
                    unit[$"equip_spellcard{slot}_user_picture_id"] = 0L;
        foreach (var picture in pictures.Where(value => consumed.Contains(value!["user_picture_id"]!.GetValue<long>())).ToArray())
            pictures.Remove(picture);
        PlayerState.Save(updated);
        return new(0, new() { ["gain_object_list"] = gains, ["remove_user_picture_id_list"] = new JsonArray(request.Ids.Select(id => (JsonNode?)JsonValue.Create(id)).ToArray()), ["user_update"] = new JsonObject { ["picture"] = updated["picture"]!.DeepClone(), ["unit"] = updated["unit"]!.DeepClone(), ["gold"] = updated["gold"]!.DeepClone(), ["phantasm_point"] = updated["phantasm_point"]!.DeepClone(), ["piece"] = updated["piece"]!.DeepClone(), ["item"] = updated["item"]!.DeepClone() } });
    }
}
