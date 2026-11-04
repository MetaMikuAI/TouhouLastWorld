using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed partial class OfflineServer
{
    private LocalReply DistributeFood(LocalFoodDistribution? request)
    {
        if (request is null || request.FoodId <= 0 || request.Value < 0 || request.Capacity <= 0 || request.EffectMinutes < 0)
            return new(503, null, "Food definition or capacity unavailable.");
        if (request.ItemId <= 0 || request.Count <= 0) return new(400, null, "Positive item and count required.");
        var updated = user.DeepClone().AsObject();
        var item = updated["item"]!["item_list"]!.AsArray().FirstOrDefault(entry => entry!["item_id"]!.GetValue<int>() == request.ItemId);
        if (item is null || item["num"]!.GetValue<int>() < request.Count) return new(409, null, "Insufficient food inventory.");
        var food = updated["lecture_room"]!["food"]!;
        var remaining = food["food_rest_value"]!.GetValue<int>();
        if (remaining < 0) return new(409, null, "Invalid stored food value.");
        var now = DateTimeOffset.UtcNow;
        if (request.EffectMinutes > 0)
        {
            var effects = food["food_list"]!.AsArray();
            var effect = effects.FirstOrDefault(entry => entry!["food_id"]!.GetValue<int>() == request.FoodId);
            var start = now;
            if (effect is not null)
            {
                if (!DateTimeOffset.TryParse(effect["add_effect_finish_time"]?.GetValue<string>(),
                    System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var finish))
                    return new(409, null, "Invalid stored food effect time.");
                if (finish > start) start = finish;
            }
            var minutes = (long)request.EffectMinutes * request.Count;
            if (minutes > (DateTimeOffset.MaxValue - start).TotalMinutes) return new(409, null, "Food effect duration exceeds limit.");
            if (effect is null)
            {
                effect = new JsonObject { ["food_id"] = request.FoodId };
                effects.Add(effect);
            }
            effect["add_effect_finish_time"] = start.AddMinutes(minutes).ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        }
        item["num"] = item["num"]!.GetValue<int>() - request.Count;
        food["food_rest_value"] = (int)Math.Min(request.Capacity, (long)remaining + (long)request.Value * request.Count);
        food["last_food_at"] = now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        Save(updated);
        return new(0, new()
        {
            ["user_update"] = new JsonObject { ["item"] = updated["item"]!.DeepClone(), ["lecture_room"] = updated["lecture_room"]!.DeepClone() }
        });
    }

    private LocalReply PurchaseFood(LocalFoodPurchase? request)
    {
        if (request is null || request.PointCost < 0 || request.OrbCost < 0)
            return new(503, null, "Food price unavailable.");
        if (request.ItemId <= 0 || request.Count <= 0) return new(400, null, "Positive item and count required.");
        var updated = user.DeepClone().AsObject();
        var points = updated["phantasm_point"]!["points"]!.GetValue<int>();
        var orb = updated["orb"]!;
        var free = orb["free"]!.GetValue<long>();
        var purchased = orb["purchased"]!.GetValue<long>();
        var pointCost = (long)request.PointCost * request.Count;
        var orbCost = (long)request.OrbCost * request.Count;
        if (points < pointCost || free < 0 || purchased < 0 || (decimal)free + purchased < orbCost)
            return new(409, null, "Insufficient local balance.");
        var entries = updated["item"]!["item_list"]!.AsArray();
        var entry = entries.FirstOrDefault(value => value!["item_id"]!.GetValue<int>() == request.ItemId);
        var count = entry?["num"]?.GetValue<int>() ?? 0;
        if (count < 0 || (long)count + request.Count > int.MaxValue)
            return new(409, null, "Item count exceeds limit.");
        if (entry is null)
        {
            entry = new JsonObject { ["item_id"] = request.ItemId };
            entries.Add(entry);
        }
        entry["num"] = count + request.Count;
        updated["phantasm_point"]!["points"] = points - (int)pointCost;
        var freeSpent = Math.Min(free, orbCost);
        orb["free"] = free - freeSpent;
        orb["purchased"] = purchased - (orbCost - freeSpent);
        Save(updated);
        return new(0, new()
        {
            ["user_update"] = new JsonObject
            {
                ["item"] = updated["item"]!.DeepClone(), ["phantasm_point"] = updated["phantasm_point"]!.DeepClone(),
                ["orb"] = orb.DeepClone()
            }
        });
    }
}
