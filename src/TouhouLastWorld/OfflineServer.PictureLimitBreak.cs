using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalPictureLimitBreak(int PictureId, int Current, int Maximum, int[] GoldCosts);

public sealed partial class OfflineServer
{
    private LocalReply LimitBreakPicture(LocalArguments? request)
    {
        if (request?.EntityId is null or <= 0 || request.Ids is not { Length: > 0 }
            || request.Ids.Any(id => id <= 0 || id == request.EntityId) || request.Ids.Distinct().Count() != request.Ids.Length)
            return new(400, null, "Invalid equipment materials.");
        var rule = request.PictureLimitBreak;
        if (rule is null || rule.PictureId <= 0 || rule.Current < 0 || rule.Maximum < rule.Current
            || rule.GoldCosts is null || rule.GoldCosts.Length != request.Ids.Length || rule.GoldCosts.Any(cost => cost < 0))
            return new(503, null, "Equipment limit definition unavailable.");
        if ((long)rule.Current + request.Ids.Length > rule.Maximum) return new(409, null, "Equipment limit exceeded.");
        var updated = user.DeepClone().AsObject();
        var pictures = updated["picture"]!["picture_list"]!.AsArray();
        var target = pictures.FirstOrDefault(entry => entry!["user_picture_id"]!.GetValue<long>() == request.EntityId);
        if (target is null) return new(404, null, "Owned equipment not found.");
        if (target["picture_id"]!.GetValue<int>() != rule.PictureId || target["potential_limitbreak"]!.GetValue<int>() != rule.Current)
            return new(409, null, "Equipment limit definition is stale.");
        var consumed = request.Ids.ToHashSet();
        if (updated["_local_idle"]?["picture_slot_list"] is JsonArray slots && slots.Any(slot =>
                consumed.Contains(slot?["user_object_id"]?.GetValue<long>() ?? 0)
                || (slot?["user_object_id"]?.GetValue<long>() == request.EntityId && slot["content"]?.GetValue<int>() == 3)))
            return new(409, null, "Equipment growth is already in progress.");
        foreach (var id in consumed)
        {
            var material = pictures.FirstOrDefault(entry => entry!["user_picture_id"]!.GetValue<long>() == id);
            if (material is null) return new(404, null, "Owned material not found.");
            if (material["picture_id"]!.GetValue<int>() != rule.PictureId || material["is_protection"]?.GetValue<int>() != 0)
                return new(409, null, "Material type or protection prevents consumption.");
        }
        var cost = rule.GoldCosts.Sum(value => (long)value);
        var gold = updated["gold"]!.GetValue<int>();
        if (gold < cost) return new(409, null, "Insufficient gold.");
        foreach (var unit in updated["unit"]!["unit_list"]!.AsArray())
            for (var slot = 1; slot <= 5; slot++)
                if (consumed.Contains(unit![$"equip_spellcard{slot}_user_picture_id"]?.GetValue<long>() ?? 0))
                    unit[$"equip_spellcard{slot}_user_picture_id"] = 0L;
        foreach (var material in pictures.Where(entry => consumed.Contains(entry!["user_picture_id"]!.GetValue<long>())).ToArray())
            pictures.Remove(material);
        target["potential_limitbreak"] = rule.Current + request.Ids.Length;
        updated["gold"] = gold - (int)cost;
        Save(updated);
        return new(0, new()
        {
            ["remove_user_picture_id_list"] = new JsonArray(request.Ids.Select(id => (JsonNode?)JsonValue.Create(id)).ToArray()),
            ["user_update"] = new JsonObject
            {
                ["picture"] = updated["picture"]!.DeepClone(), ["unit"] = updated["unit"]!.DeepClone(), ["gold"] = gold - (int)cost
            }
        });
    }
}
