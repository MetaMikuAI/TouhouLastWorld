using System.Text.Json.Nodes;
using System.Text;
using System.Text.Json;
using System.Globalization;
using System.Security.Cryptography;

namespace TouhouLastWorld;

internal sealed class FacilitiesService
{
    internal FacilitiesService(PlayerState playerState)
    {
        PlayerState = playerState;
    }

    private readonly PlayerState PlayerState;
    internal int LectureExpansionRank => PlayerState.user["lecture_room"]!["unit"]!["expansion_rank"]!.GetValue<int>();
    internal int LectureFoodExpansionRank => PlayerState.user["lecture_room"]!["food"]!["expansion_rank"]!.GetValue<int>();
    internal int TrainingExpansionRank => PlayerState.user["training_room"]!["expansion_rank"]!.GetValue<int>();

    internal LocalReply ShortenTraining(ShortenTrainingCommand? request)
    {
        var materials = request?.TrainingMaterials;
        if (request?.EntityId is null or <= 0 || materials is null || materials.Length == 0 || materials.Any(entry => entry.ItemId <= 0 || entry.Count <= 0) || materials.Select(entry => entry.ItemId).Distinct().Count() != materials.Length)
            return new(400, null, "Distinct positive training materials required.");
        if (materials.Any(entry => entry.Seconds <= 0))
            return new(503, null, "Material duration unavailable.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var room = updated["training_room"]!.AsObject();
        var task = room["unit_list"]!.AsArray().FirstOrDefault(entry => entry!["user_unit_id"]!.GetValue<long>() == request.EntityId);
        if (task is null)
            return new(404, null, "Training task not found.");
        if (!DateTimeOffset.TryParse(task["training_finish_time"]?.GetValue<string>(), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var finish))
            return new(409, null, "Invalid training finish time.");
        var now = PlayerState.Clock.GetUtcNow();
        if (finish <= now)
            return new(409, null, "Training already elapsed.");
        var inventory = updated["item"]!["item_list"]!.AsArray();
        decimal seconds = 0;
        foreach (var material in materials)
        {
            var item = inventory.FirstOrDefault(entry => entry!["item_id"]!.GetValue<int>() == material.ItemId);
            if (item is null || item["num"]!.GetValue<int>() < material.Count)
                return new(409, null, "Insufficient training materials.");
            item["num"] = item["num"]!.GetValue<int>() - material.Count;
            seconds += (decimal)material.Seconds * material.Count;
        }

        var reduced = seconds >= (decimal)(finish - now).TotalSeconds ? now : finish.AddSeconds(-(double)seconds);
        var finishText = reduced.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        task["training_finish_time"] = finishText;
        PlayerState.Save(updated);
        return new(0, new() { ["finish_time"] = finishText, ["user_update"] = new JsonObject { ["training_room"] = room.DeepClone(), ["item"] = updated["item"]!.DeepClone() } });
    }

    internal LocalReply StartTraining(LocalTrainingStart? request)
    {
        if (request is null || request.Capacity <= 0 || request.Seconds <= 0)
            return new(503, null, "Training definition unavailable.");
        if (request.UnitId <= 0 || request.ItemId <= 0 || request.Skill is < 1 or > 3 || request.Slot < 1 || request.Slot > request.Capacity)
            return new(400, null, "Invalid training selection.");
        var updated = PlayerState.user.DeepClone().AsObject();
        if (!updated["unit"]!["unit_list"]!.AsArray().Any(entry => entry!["user_unit_id"]!.GetValue<long>() == request.UnitId))
            return new(404, null, "Owned unit not found.");
        var room = updated["training_room"]!.AsObject();
        var entries = room["unit_list"]!.AsArray();
        if (entries.Any(entry => entry!["slot_number"]!.GetValue<int>() == request.Slot || entry["user_unit_id"]!.GetValue<long>() == request.UnitId))
            return new(409, null, "Training slot or unit already in use.");
        var item = updated["item"]!["item_list"]!.AsArray().FirstOrDefault(entry => entry!["item_id"]!.GetValue<int>() == request.ItemId);
        if (item is null || item["num"]!.GetValue<int>() <= 0)
            return new(409, null, "Training material unavailable.");
        var now = PlayerState.Clock.GetUtcNow().UtcDateTime;
        entries.Add(new JsonObject { ["slot_number"] = request.Slot, ["user_unit_id"] = request.UnitId, ["skill_number"] = request.Skill, ["item_id"] = request.ItemId, ["training_start_time"] = now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture), ["training_finish_time"] = now.AddSeconds(request.Seconds).ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) });
        item["num"] = item["num"]!.GetValue<int>() - 1;
        PlayerState.Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["training_room"] = room.DeepClone(), ["item"] = updated["item"]!.DeepClone() } });
    }

    internal LocalReply CancelDispatch(long? id)
    {
        if (id is null or <= 0)
            return new(400, null, "Positive dispatch ID required.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var dispatch = updated["dispatch"]!.AsObject();
        var entries = dispatch["dispatch_list"]!.AsArray();
        var entry = entries.FirstOrDefault(value => value!["user_dispatch_id"]!.GetValue<long>() == id);
        if (entry is null)
            return new(404, null, "Dispatch task not found.");
        entries.Remove(entry);
        PlayerState.Save(updated);
        return new(0, new() { ["remove_user_dispatch_id"] = id.Value, ["user_update"] = new JsonObject { ["dispatch"] = dispatch.DeepClone() } });
    }

    internal LocalReply CancelTraining(int? slot)
    {
        if (slot is null or <= 0)
            return new(400, null, "Positive training slot required.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var room = updated["training_room"]!.AsObject();
        var entries = room["unit_list"]!.AsArray();
        var entry = entries.FirstOrDefault(value => value!["slot_number"]!.GetValue<int>() == slot);
        if (entry is null)
            return new(404, null, "Training task not found.");
        entries.Remove(entry);
        PlayerState.Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["training_room"] = room.DeepClone() } });
    }

    internal LocalReply ExpandFacility(ExpandFacilityCommand? request, string field, string? section = null)
    {
        if (request?.EntityId is null or <= 0 or > int.MaxValue || request.Value is null or < 0)
            return new(503, null, "Next facility level unavailable.");
        var current = section is null ? PlayerState.user[field]! : PlayerState.user[field]![section]!;
        var rank = current["expansion_rank"]!.GetValue<int>();
        if (request.EntityId != (long)rank + 1)
            return new(409, null, "Facility level changed.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var orb = updated["orb"]!.AsObject();
        var free = orb["free"]!.GetValue<long>();
        var purchased = orb["purchased"]!.GetValue<long>();
        var cost = request.Value.Value;
        if (free < 0 || purchased < 0 || (decimal)free + purchased < cost)
            return new(409, null, "Insufficient local balance.");
        var freeSpent = Math.Min(free, cost);
        orb["free"] = free - freeSpent;
        orb["purchased"] = purchased - (cost - freeSpent);
        var facility = section is null ? updated[field]! : updated[field]![section]!;
        facility["expansion_rank"] = (int)request.EntityId.Value;
        PlayerState.Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { [field] = updated[field]!.DeepClone(), ["orb"] = orb.DeepClone() } });
    }

    internal LocalReply AddLectureUnit(AddLectureUnitCommand? request)
    {
        if (request?.EntityId is null or <= 0)
            return new(400, null, "Positive unit ID required.");
        if (request.Value is null or <= 0)
            return new(503, null, "Facility capacity unavailable.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var unit = updated["unit"]!["unit_list"]!.AsArray().FirstOrDefault(entry => entry!["user_unit_id"]!.GetValue<long>() == request.EntityId);
        if (unit is null)
            return new(404, null, "Owned unit not found.");
        var room = updated["lecture_room"]!.AsObject();
        var entries = room["unit"]!["unit_list"]!.AsArray();
        if (!entries.Any(entry => entry!["user_unit_id"]!.GetValue<long>() == request.EntityId))
        {
            if (entries.Count >= request.Value)
                return new(409, null, "Facility is full.");
            entries.Add(new JsonObject { ["user_unit_id"] = request.EntityId.Value, ["put_trust_up_icon"] = 0 });
        }

        unit["is_lecture"] = 1;
        PlayerState.Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["lecture_room"] = room.DeepClone(), ["unit"] = updated["unit"]!.DeepClone() } });
    }

    internal LocalReply RemoveLectureUnit(long? id)
    {
        if (id is null or <= 0)
            return new(400, null, "Positive unit ID required.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var unit = updated["unit"]!["unit_list"]!.AsArray().FirstOrDefault(entry => entry!["user_unit_id"]!.GetValue<long>() == id);
        if (unit is null)
            return new(404, null, "Owned unit not found.");
        var room = updated["lecture_room"]!.AsObject();
        var entries = room["unit"]!["unit_list"]!.AsArray();
        for (var index = entries.Count - 1; index >= 0; index--)
            if (entries[index]!["user_unit_id"]!.GetValue<long>() == id)
                entries.RemoveAt(index);
        unit["is_lecture"] = 0;
        PlayerState.Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["lecture_room"] = room.DeepClone(), ["unit"] = updated["unit"]!.DeepClone() } });
    }

    internal LocalReply DistributeFood(LocalFoodDistribution? request)
    {
        if (request is null || request.FoodId <= 0 || request.Value < 0 || request.Capacity <= 0 || request.EffectMinutes < 0)
            return new(503, null, "Food definition or capacity unavailable.");
        if (request.ItemId <= 0 || request.Count <= 0)
            return new(400, null, "Positive item and count required.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var item = updated["item"]!["item_list"]!.AsArray().FirstOrDefault(entry => entry!["item_id"]!.GetValue<int>() == request.ItemId);
        if (item is null || item["num"]!.GetValue<int>() < request.Count)
            return new(409, null, "Insufficient food inventory.");
        var food = updated["lecture_room"]!["food"]!;
        var remaining = food["food_rest_value"]!.GetValue<int>();
        if (remaining < 0)
            return new(409, null, "Invalid stored food value.");
        var now = PlayerState.Clock.GetUtcNow();
        if (request.EffectMinutes > 0)
        {
            var effects = food["food_list"]!.AsArray();
            var effect = effects.FirstOrDefault(entry => entry!["food_id"]!.GetValue<int>() == request.FoodId);
            var start = now;
            if (effect is not null)
            {
                if (!DateTimeOffset.TryParse(effect["add_effect_finish_time"]?.GetValue<string>(), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var finish))
                    return new(409, null, "Invalid stored food effect time.");
                if (finish > start)
                    start = finish;
            }

            var minutes = (long)request.EffectMinutes * request.Count;
            if (minutes > (DateTimeOffset.MaxValue - start).TotalMinutes)
                return new(409, null, "Food effect duration exceeds limit.");
            if (effect is null)
            {
                effect = new JsonObject
                {
                    ["food_id"] = request.FoodId
                };
                effects.Add(effect);
            }

            effect["add_effect_finish_time"] = start.AddMinutes(minutes).ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        }

        item["num"] = item["num"]!.GetValue<int>() - request.Count;
        food["food_rest_value"] = (int)Math.Min(request.Capacity, (long)remaining + (long)request.Value * request.Count);
        food["last_food_at"] = now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        PlayerState.Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["item"] = updated["item"]!.DeepClone(), ["lecture_room"] = updated["lecture_room"]!.DeepClone() } });
    }

    internal LocalReply PurchaseFood(LocalFoodPurchase? request)
    {
        if (request is null || request.PointCost < 0 || request.OrbCost < 0)
            return new(503, null, "Food price unavailable.");
        if (request.ItemId <= 0 || request.Count <= 0)
            return new(400, null, "Positive item and count required.");
        var updated = PlayerState.user.DeepClone().AsObject();
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
            entry = new JsonObject
            {
                ["item_id"] = request.ItemId
            };
            entries.Add(entry);
        }

        entry["num"] = count + request.Count;
        updated["phantasm_point"]!["points"] = points - (int)pointCost;
        var freeSpent = Math.Min(free, orbCost);
        orb["free"] = free - freeSpent;
        orb["purchased"] = purchased - (orbCost - freeSpent);
        PlayerState.Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["item"] = updated["item"]!.DeepClone(), ["phantasm_point"] = updated["phantasm_point"]!.DeepClone(), ["orb"] = orb.DeepClone() } });
    }

    internal const string SpiritStock = "_local_gathering_phantasm_point";
    internal const string GoldStock = "_local_gathering_gold";
    internal void RefreshFacilityStock(RefreshFacilityStockCommand? request)
    {
        if (request?.GoldIncome is null && request?.SpiritIncome is null)
            return;
        var updated = PlayerState.user.DeepClone().AsObject();
        var goldChanged = AccrueBox(updated, "gold_box", GoldStock, request.GoldIncome);
        var spiritChanged = AccrueBox(updated, "phantasm_box", SpiritStock, request.SpiritIncome);
        if (goldChanged || spiritChanged)
            PlayerState.Save(updated);
    }

    internal static bool AccrueBox(JsonObject updated, string section, string stockKey, LocalBoxIncome? rule)
    {
        if (rule is not { Level: > 0, Interval: > 0, Yield: > 0, Capacity: > 0, At: > 0 } || updated[section]?["level"]?.GetValue<int>() != rule.Level)
            return false;
        var timestampKey = stockKey + "_at";
        var timestamp = updated[timestampKey]?.GetValue<long>();
        var last = timestamp ?? rule.At;
        var stock = updated[stockKey]?.GetValue<int>() ?? 0;
        if (last < 0 || last > rule.At || stock < 0)
            return false;
        var intervals = (rule.At - last) / rule.Interval;
        var remaining = Math.Max(0L, (long)rule.Capacity - stock);
        var units = Math.Min(remaining, Math.Min(intervals, remaining) * rule.Yield);
        var refreshed = stock + (int)units;
        // 库存满时停止累计，收取后从当前时间继续。
        var next = refreshed >= rule.Capacity ? rule.At : last + intervals * rule.Interval;
        if (timestamp == next && refreshed == stock)
            return false;
        updated[timestampKey] = next;
        updated[stockKey] = refreshed;
        return true;
    }

    internal LocalReply GatherPoints(GatherPointsCommand? request)
    {
        if (request?.EntityId is null or <= 0 or > int.MaxValue || request.Value != PlayerState.user["level"]?.GetValue<int>())
            return new(503, null, "Current level capacity unavailable.");
        if (PlayerState.user["phantasm_point"] is not JsonObject points)
            return new(503, null, "Local point balance unavailable.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var accrued = AccrueBox(updated, "phantasm_box", SpiritStock, request.SpiritIncome);
        var stock = updated[SpiritStock]?.GetValue<int>() ?? 0;
        var balance = points["points"]!.GetValue<int>();
        if (stock < 0 || balance < 0)
            return new(503, null, "Invalid local balance.");
        var amount = (int)Math.Min(stock, Math.Max(0, request.EntityId.Value - balance));
        if (amount != 0)
        {
            updated["phantasm_point"]!["points"] = balance + amount;
            updated[SpiritStock] = stock - amount;
        }

        if (accrued || amount != 0)
            PlayerState.Save(updated);
        return new(0, new() { ["gathering_phantasm_point"] = stock - amount, ["user_update"] = new JsonObject { ["phantasm_point"] = PlayerState.user["phantasm_point"]!.DeepClone() } });
    }

    internal LocalReply GatherGold(GatherGoldCommand? request)
    {
        if (request?.EntityId is null or <= 0 or > int.MaxValue || request.Value != PlayerState.user["level"]?.GetValue<int>())
            return new(503, null, "Current level capacity unavailable.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var accrued = AccrueBox(updated, "gold_box", GoldStock, request.GoldIncome);
        var stock = updated[GoldStock]?.GetValue<int>() ?? 0;
        var balance = PlayerState.user["gold"]!.GetValue<int>();
        if (stock < 0 || balance < 0)
            return new(503, null, "Invalid local balance.");
        var amount = (int)Math.Min(stock, Math.Max(0, request.EntityId.Value - balance));
        if (amount != 0)
        {
            updated["gold"] = balance + amount;
            updated[GoldStock] = stock - amount;
        }

        if (accrued || amount != 0)
            PlayerState.Save(updated);
        return new(0, new() { ["gathering_gold"] = stock - amount, ["user_update"] = new JsonObject { ["gold"] = balance + amount } });
    }

    internal LocalReply ReceiveIdleDrop()
    {
        if (PlayerState.user["_local_idle"]?["drop_list"] is JsonArray { Count: > 0 })
            return new(503, null, "Stored idle reward settlement unavailable.");
        return new(0, new() { ["drop_list"] = new JsonArray(), ["search_elapsed_time"] = 0, ["search_max_time"] = 43200 });
    }

    internal LocalIdleSet? IdleSelection(int type, int number)
    {
        if (type is not (9 or 10) || number is < 1 or > 5)
            return null;
        var slots = PlayerState.user["_local_idle"]?[type == 9 ? "unit_slot_list" : "picture_slot_list"] as JsonArray;
        var slot = slots?.FirstOrDefault(entry => entry?["slot_number"]?.GetValue<int>() == number);
        return slot is null ? null : new(slot["user_object_id"]!.GetValue<long>(), type, number, slot["content"]!.GetValue<int>(), 0);
    }

    internal LocalReply CompleteIdleSlot(CompleteIdleSlotCommand? arguments)
    {
        if (arguments?.Value is not (9 or 10) || arguments.EntityId is null or < 1 or > 5)
            return new(400, null, "Invalid idle completion selection.");
        var selection = IdleSelection(arguments.Value.Value, (int)arguments.EntityId.Value);
        if (selection is null)
            return new(404, null, "Idle slot not found.");
        var request = arguments.Idle;
        if (request is null || request.GrowthField is null)
            return new(503, null, "Idle growth definition unavailable.");
        if (request.ObjectId != selection.ObjectId || request.Content != selection.Content || request.ObjectType != selection.ObjectType || request.Slot != selection.Slot || !request.Eligible)
            return new(409, null, "Idle completion state changed.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var slots = updated["_local_idle"]![selection.ObjectType == 9 ? "unit_slot_list" : "picture_slot_list"]!.AsArray();
        var slot = slots.First(entry => entry!["slot_number"]!.GetValue<int>() == selection.Slot)!;
        if (!DateTimeOffset.TryParse(slot["end_at"]?.GetValue<string>(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var end) || end > PlayerState.Clock.GetUtcNow())
            return new(409, null, "Idle growth has not finished.");
        var key = selection.ObjectType == 9 ? "unit" : "picture";
        var entry = updated[key]![key + "_list"]!.AsArray().FirstOrDefault(value => value?["user_" + key + "_id"]?.GetValue<long>() == selection.ObjectId);
        if (entry is null)
            return new(404, null, "Owned idle object not found.");
        if (request.CurrentValue is < 0 or int.MaxValue || entry[request.GrowthField]?.GetValue<int>() != request.CurrentValue || slot["_local_growth_field"]?.GetValue<string>() != request.GrowthField || slot["_local_growth_value"]?.GetValue<int>() != request.CurrentValue)
            return new(409, null, "Idle growth snapshot changed or unavailable.");
        entry[request.GrowthField] = request.CurrentValue + 1;
        slots.Remove(slot);
        PlayerState.Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { [key] = updated[key]!.DeepClone() } });
    }

    internal int IdleStage(int content)
    {
        var stages = PlayerState.user["_local_idle"]?["content_stage_list"] as JsonArray;
        return content > 0 && stages is not null && content <= stages.Count ? stages[content - 1]!.GetValue<int>() : 0;
    }

    internal JsonObject IdleOverview() => new()
    {
        ["content_stage_list"] = PlayerState.user["_local_idle"]?["content_stage_list"]?.DeepClone() ?? new JsonArray(),
        ["before_content_stage_list"] = PlayerState.user["_local_idle"]?["content_stage_list"]?.DeepClone() ?? new JsonArray(),
        ["unit_slot_list"] = PlayerState.user["_local_idle"]?["unit_slot_list"]?.DeepClone() ?? new JsonArray(),
        ["picture_slot_list"] = PlayerState.user["_local_idle"]?["picture_slot_list"]?.DeepClone() ?? new JsonArray(),
        ["drop_list"] = new JsonArray(),
        ["search_elapsed_time"] = 0,
        ["search_max_time"] = 43200
    };
    internal LocalReply SetIdleSlot(LocalIdleSet? request)
    {
        if (request is null || request.ObjectId <= 0 || request.ObjectType is not (9 or 10) || request.Slot is < 1 or > 5 || request.Cancel is not (0 or 1) || (request.ObjectType == 9 ? request.Content is not (2 or 4) : request.Content is not (3 or 7)))
            return new(400, null, "Invalid idle slot selection.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var idle = updated["_local_idle"] as JsonObject ?? new JsonObject();
        if (idle.Parent is null)
            updated["_local_idle"] = idle;
        var key = request.ObjectType == 9 ? "unit_slot_list" : "picture_slot_list";
        var slots = idle[key] as JsonArray ?? new JsonArray();
        if (slots.Parent is null)
            idle[key] = slots;
        var slot = slots.FirstOrDefault(entry => entry?["slot_number"]?.GetValue<int>() == request.Slot);
        if (request.Cancel == 1)
        {
            if (slot is null)
                return new(404, null, "Idle slot not found.");
            if (slot["user_object_id"]!.GetValue<long>() != request.ObjectId || slot["content"]!.GetValue<int>() != request.Content)
                return new(409, null, "Idle slot selection changed.");
            slots.Remove(slot);
            PlayerState.Save(updated);
            return new(0, new() { ["start_at"] = "", ["end_at"] = "" });
        }

        if (request.Capacity is < 1 or > 5 || request.Seconds <= 0 || request.GrowthField is null)
            return new(503, null, "Idle growth definition unavailable.");
        if (request.Slot > request.Capacity || !request.Eligible)
            return new(409, null, "Idle slot or growth unavailable.");
        var owned = request.ObjectType == 9 ? updated["unit"]?["unit_list"] : updated["picture"]?["picture_list"];
        var idKey = request.ObjectType == 9 ? "user_unit_id" : "user_picture_id";
        var entry = (owned as JsonArray)?.FirstOrDefault(value => value?[idKey]?.GetValue<long>() == request.ObjectId);
        if (entry is null)
            return new(404, null, "Owned idle object not found.");
        if (entry[request.GrowthField]?.GetValue<int>() != request.CurrentValue)
            return new(409, null, "Idle growth state changed.");
        if (slot is not null || slots.Any(value => value?["user_object_id"]?.GetValue<long>() == request.ObjectId))
            return new(409, null, "Idle slot or object already in use.");
        var now = PlayerState.Clock.GetUtcNow().UtcDateTime;
        if (request.Seconds > (DateTime.MaxValue - now).TotalSeconds)
            return new(400, null, "Idle duration exceeds limit.");
        var start = now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        var end = now.AddSeconds(request.Seconds).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        slots.Add(new JsonObject { ["slot_number"] = request.Slot, ["object_type"] = request.ObjectType, ["object_id"] = entry[request.ObjectType == 9 ? "unit_id" : "picture_id"]!.DeepClone(), ["user_object_id"] = request.ObjectId, ["content"] = request.Content, ["start_at"] = start, ["end_at"] = end, ["_local_growth_field"] = request.GrowthField, ["_local_growth_value"] = request.CurrentValue });
        PlayerState.Save(updated);
        return new(0, new() { ["start_at"] = start, ["end_at"] = end });
    }

    internal LocalReply StartBoxRenovation(string section, LocalBoxRenovation? rule)
    {
        if (PlayerState.user[section] is not JsonObject box)
            return new(503, null, "Local facility unavailable.");
        var level = box["level"]!.GetValue<int>();
        if (rule is null || level <= 0 || level == int.MaxValue || rule.CurrentLevel != level || rule.NextLevel != level + 1 || rule.RequiredLevel <= 0 || rule.GoldCost < 0 || rule.Seconds < 0)
            return new(503, null, "Next facility level unavailable.");
        if (!string.IsNullOrEmpty(box["renovation_finish_time"]?.GetValue<string>()))
            return new(409, null, "Renovation already in progress.");
        if (PlayerState.user["level"]!.GetValue<int>() < rule.RequiredLevel)
            return new(409, null, "Required account level not reached.");
        var gold = PlayerState.user["gold"]!.GetValue<int>();
        if (gold < rule.GoldCost)
            return new(409, null, "Insufficient gold.");
        var updated = PlayerState.user.DeepClone().AsObject();
        updated["gold"] = gold - rule.GoldCost;
        updated[section]!["renovation_finish_time"] = PlayerState.Clock.GetUtcNow().AddSeconds(rule.Seconds).ToString("O", CultureInfo.InvariantCulture);
        PlayerState.Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["gold"] = gold - rule.GoldCost, [section] = PlayerState.user[section]!.DeepClone() } });
    }

    internal LocalReply CompleteBoxRenovation(string section, CompleteBoxRenovationCommand? request)
    {
        if (PlayerState.user[section] is not JsonObject box)
            return new(503, null, "Local facility unavailable.");
        var finish = box["renovation_finish_time"]?.GetValue<string>();
        if (string.IsNullOrEmpty(finish))
            return new(409, null, "No renovation in progress.");
        if (!DateTimeOffset.TryParse(finish, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var deadline))
            return new(503, null, "Invalid renovation time.");
        if (deadline > PlayerState.Clock.GetUtcNow())
            return new(409, null, "Renovation is not finished.");
        var level = box["level"]!.GetValue<int>();
        if (level <= 0 || level == int.MaxValue || request?.Value != level || request.EntityId != (long)level + 1)
            return new(503, null, "Next facility level unavailable.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var gold = section == "gold_box";
        var stockKey = gold ? GoldStock : SpiritStock;
        var income = gold ? request.GoldIncome : request.SpiritIncome;
        AccrueBox(updated, section, stockKey, income);
        if (income is not null && updated[stockKey + "_at"]?.GetValue<long>() <= income.At)
            updated[stockKey + "_at"] = income.At;
        updated[section]!["level"] = level + 1;
        updated[section]!["renovation_finish_time"] = "";
        PlayerState.Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { [section] = PlayerState.user[section]!.DeepClone() } });
    }
}
