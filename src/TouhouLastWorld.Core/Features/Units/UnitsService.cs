using System.Text.Json.Nodes;
using System.Text;
using System.Text.Json;
using System.Globalization;
using System.Security.Cryptography;

namespace TouhouLastWorld;

internal sealed class UnitsService
{
    internal UnitsService(PlayerState playerState)
    {
        PlayerState = playerState;
    }

    private readonly PlayerState PlayerState;
    internal LocalReply AwakenUnit(AwakenUnitCommand? request)
    {
        if (request?.EntityId is null or <= 0)
            return new(400, null, "Invalid unit.");
        var rule = request.Awakening;
        if (rule is null || rule.CurrentRare <= 0 || rule.CurrentRare == int.MaxValue || rule.NextRare != rule.CurrentRare + 1 || rule.PieceCost < 0)
            return new(503, null, "Awakening definition unavailable.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var unit = updated["unit"]!["unit_list"]!.AsArray().FirstOrDefault(entry => entry!["user_unit_id"]!.GetValue<long>() == request.EntityId);
        if (unit is null)
            return new(404, null, "Owned unit not found.");
        if (unit["rare"]!.GetValue<int>() != rule.CurrentRare)
            return new(409, null, "Awakening definition is stale.");
        if (updated["_local_idle"]?["unit_slot_list"] is JsonArray slots && slots.Any(slot => slot?["user_object_id"]?.GetValue<long>() == request.EntityId && slot["content"]?.GetValue<int>() == 4))
            return new(409, null, "Unit awakening is already in progress.");
        var piece = updated["piece"]!.GetValue<int>();
        if (piece < rule.PieceCost)
            return new(409, null, "Insufficient pieces.");
        updated["piece"] = piece - rule.PieceCost;
        unit["rare"] = rule.NextRare;
        if (rule.PieceCost > 0)
            updated[MissionsService.UnitAwakeningMilestone] = true;
        PlayerState.Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["piece"] = piece - rule.PieceCost, ["unit"] = updated["unit"]!.DeepClone() } });
    }

    internal LocalReply IncreaseDestiny(IncreaseDestinyCommand? request)
    {
        if (request?.EntityId is null or <= 0)
            return new(400, null, "Invalid unit.");
        if (request.Ids is { Length: > 0 })
            return new(503, null, "Unit materials not supported.");
        var rule = request.DestinyGrowth;
        if (rule is null || rule.UnitId <= 0 || rule.Current < 0 || rule.Maximum <= 0 || rule.Materials is not { Length: 3 })
            return new(503, null, "Growth definition unavailable.");
        if (rule.Materials.Any(material => material.Count < 0) || rule.Materials.All(material => material.Count == 0))
            return new(400, null, "Invalid material count.");
        if (rule.Materials.Any(material => material.Count > 0 && (material.ItemId <= 0 || material.Value <= 0)))
            return new(503, null, "Material definition unavailable.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var unit = updated["unit"]!["unit_list"]!.AsArray().FirstOrDefault(entry => entry!["user_unit_id"]!.GetValue<long>() == request.EntityId);
        if (unit is null)
            return new(404, null, "Owned unit not found.");
        if (unit["unit_id"]!.GetValue<int>() != rule.UnitId || unit["destiny_value"]!.GetValue<int>() != rule.Current)
            return new(409, null, "Growth definition is stale.");
        if (rule.Current >= rule.Maximum)
            return new(409, null, "Growth limit reached.");
        if (updated["_local_idle"]?["unit_slot_list"] is JsonArray slots && slots.Any(slot => slot?["user_object_id"]?.GetValue<long>() == request.EntityId && slot["content"]?.GetValue<int>() == 2))
            return new(409, null, "Unit growth is already in progress.");
        long value = rule.Current;
        foreach (var material in rule.Materials.Where(material => material.Count > 0))
        {
            var item = updated["item"]!["item_list"]!.AsArray().FirstOrDefault(entry => entry!["item_id"]!.GetValue<int>() == material.ItemId);
            if (item is null || item["num"]!.GetValue<int>() < material.Count)
                return new(409, null, "Insufficient materials.");
            item["num"] = item["num"]!.GetValue<int>() - material.Count;
            value = Math.Min(rule.Maximum, value + (long)material.Count * material.Value);
        }

        unit["destiny_value"] = (int)Math.Max(1, value);
        PlayerState.Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["unit"] = updated["unit"]!.DeepClone(), ["item"] = updated["item"]!.DeepClone() } });
    }

    internal LocalReply LimitBreakUnit(LimitBreakUnitCommand? request)
    {
        if (request?.EntityId is null or <= 0 || request.Value is null or < 0)
            return new(400, null, "Invalid unit or material count.");
        if (request.Ids is { Length: > 0 })
            return new(503, null, "Unit materials not supported.");
        var rule = request.LimitBreak;
        if (rule is null || rule.UnitId <= 0 || rule.CurrentStep < 0 || rule.CurrentStep == int.MaxValue || rule.NextStep != rule.CurrentStep + 1 || rule.RequiredLevel <= 0 || rule.GoldCost < 0 || rule.MaterialCount <= 0 || rule.ItemId <= 0 || rule.DestinyPerItem < 0 || rule.DestinyMaximum <= 0)
            return new(503, null, "Limit break definition unavailable.");
        if (request.Value != rule.MaterialCount)
            return new(400, null, "Material count does not match definition.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var unit = updated["unit"]!["unit_list"]!.AsArray().FirstOrDefault(entry => entry!["user_unit_id"]!.GetValue<long>() == request.EntityId);
        if (unit is null)
            return new(404, null, "Owned unit not found.");
        if (unit["unit_id"]!.GetValue<int>() != rule.UnitId || unit["level_limitbreak"]!.GetValue<int>() != rule.CurrentStep)
            return new(409, null, "Limit break definition is stale.");
        if (unit["level"]!.GetValue<int>() < rule.RequiredLevel)
            return new(409, null, "Required unit level not reached.");
        var item = updated["item"]!["item_list"]!.AsArray().FirstOrDefault(entry => entry!["item_id"]!.GetValue<int>() == rule.ItemId);
        var gold = updated["gold"]!.GetValue<int>();
        if (gold < rule.GoldCost || item is null || item["num"]!.GetValue<int>() < rule.MaterialCount)
            return new(409, null, "Insufficient gold or materials.");
        var destiny = unit["destiny_value"]!.GetValue<int>();
        if (destiny < 0 || destiny > rule.DestinyMaximum)
            return new(503, null, "Invalid destiny value.");
        unit["destiny_value"] = (int)Math.Clamp(destiny + (long)rule.MaterialCount * rule.DestinyPerItem, 1, rule.DestinyMaximum);
        unit["level_limitbreak"] = rule.NextStep;
        updated[MissionsService.UnitLimitBreakMilestone] = true;
        updated["gold"] = gold - rule.GoldCost;
        item["num"] = item["num"]!.GetValue<int>() - rule.MaterialCount;
        PlayerState.Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["gold"] = gold - rule.GoldCost, ["item"] = updated["item"]!.DeepClone(), ["unit"] = updated["unit"]!.DeepClone() } });
    }

    internal LocalReply AllocatePoints(LocalPointAllocation? request)
    {
        if (request is null || request.UnitId <= 0 || request.Targets is not { Length: > 0 and <= 9 } || request.Targets.Any(entry => entry.Type is < 1 or > 9 || entry.Step is < 1 or > int.MaxValue) || request.Targets.Select(entry => entry.Type).Distinct().Count() != request.Targets.Length)
            return new(400, null, "Distinct valid allocation targets required.");
        if (request.Rules is null || request.Rules.Length == 0)
            return new(503, null, "Allocation rules unavailable.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var unit = updated["unit"]!["unit_list"]!.AsArray().FirstOrDefault(entry => entry!["user_unit_id"]!.GetValue<long>() == request.UnitId);
        if (unit is null)
            return new(404, null, "Owned unit not found.");
        if (unit["point_allocation_info"] is not JsonObject points)
            return new(409, null, "Point allocation state unavailable.");
        var remaining = points["pa_points"]!.GetValue<int>();
        var used = points["pa_total_used_points"]!.GetValue<int>();
        if (remaining < 0 || used < 0)
            return new(409, null, "Invalid point balance.");
        decimal cost = 0;
        foreach (var target in request.Targets)
        {
            var field = $"pa_type_{target.Type}";
            var current = points[field]?.GetValue<int>();
            if (current is null or < 0 || target.Step < current)
                return new(409, null, "Allocation cannot decrease without reset.");
            var path = request.Rules.Where(rule => rule.Type == target.Type && rule.Step > current && rule.Step <= target.Step).OrderBy(rule => rule.Step).ToArray();
            if (path.Length != target.Step - current)
                return new(503, null, "Allocation path unavailable.");
            long nextStep = current.Value + 1L;
            foreach (var rule in path)
            {
                if (rule.Step != nextStep++ || rule.Cost < 0 || rule.RequiredUsed < 0)
                    return new(503, null, "Invalid allocation rule.");
                if (rule.RequiredUsed > used)
                    return new(403, null, "Allocation prerequisite not met.");
                cost += rule.Cost;
            }

            points[field] = (int)target.Step;
        }

        if (cost > remaining || cost + used > int.MaxValue)
            return new(409, null, "Insufficient or invalid point balance.");
        points["pa_points"] = remaining - (int)cost;
        points["pa_total_used_points"] = used + (int)cost;
        PlayerState.Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["unit"] = updated["unit"]!.DeepClone() } });
    }

    internal LocalReply ResetPoints(LocalPointReset? request)
    {
        if (request is null || request.GoldCost < 0)
            return new(503, null, "Point reset settings unavailable.");
        if (request.UnitId <= 0)
            return new(400, null, "Positive unit ID required.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var unit = updated["unit"]!["unit_list"]!.AsArray().FirstOrDefault(entry => entry!["user_unit_id"]!.GetValue<long>() == request.UnitId);
        if (unit is null)
            return new(404, null, "Owned unit not found.");
        if (unit["point_allocation_info"] is not JsonObject points)
            return new(409, null, "Point allocation state unavailable.");
        var remaining = points["pa_points"]!.GetValue<int>();
        var used = points["pa_total_used_points"]!.GetValue<int>();
        if (remaining < 0 || used < 0 || (long)remaining + used > int.MaxValue)
            return new(409, null, "Invalid point balance.");
        var types = Enumerable.Range(1, 9).Select(index => $"pa_type_{index}").ToArray();
        if (types.Any(field => points[field] is null || points[field]!.GetValue<int>() < 0))
            return new(409, null, "Invalid point allocation.");
        if (used == 0)
            return types.All(field => points[field]!.GetValue<int>() == 0) ? new(0, new()) : new(409, null, "Allocation total does not match its entries.");
        var cost = request.FreeReset || points["is_pa_reset_not_free"]!.GetValue<int>() == 0 ? 0 : request.GoldCost;
        var gold = updated["gold"]!.GetValue<int>();
        if (gold < cost)
            return new(409, null, "Insufficient local balance.");
        foreach (var field in types)
            points[field] = 0;
        points["pa_points"] = remaining + used;
        points["pa_total_used_points"] = 0;
        points["is_pa_reset_not_free"] = 1;
        updated["gold"] = gold - cost;
        PlayerState.Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["unit"] = updated["unit"]!.DeepClone(), ["gold"] = gold - cost } });
    }

    internal LocalReply PromoteRank(PromoteRankCommand? request)
    {
        if (request?.EntityId is null or <= 0)
            return new(400, null, "Invalid unit.");
        var rule = request.RankPromotion;
        if (rule is null || rule.UnitId <= 0 || rule.Rank <= 0 || rule.Rank == int.MaxValue || rule.NextRank != rule.Rank + 1 || rule.RequiredLevel <= 0 || rule.GoldCost < 0 || rule.Materials is not { Length: 6 } || rule.Materials.Any(material => material.UnitId != rule.UnitId || material.Rank != rule.Rank || material.Count <= 0))
            return new(503, null, "Rank promotion definition unavailable.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var unit = updated["unit"]!["unit_list"]!.AsArray().FirstOrDefault(entry => entry!["user_unit_id"]!.GetValue<long>() == request.EntityId);
        if (unit is null)
            return new(404, null, "Owned unit not found.");
        if (unit["unit_id"]!.GetValue<int>() != rule.UnitId || unit["rank"]!.GetValue<int>() != rule.Rank)
            return new(409, null, "Rank promotion definition is stale.");
        if (unit["level"]!.GetValue<int>() < rule.RequiredLevel)
            return new(409, null, "Required unit level not reached.");
        long goldCost = rule.GoldCost;
        var items = updated["item"]!["item_list"]!.AsArray();
        for (var index = 0; index < 6; index++)
        {
            var filled = unit[$"is_set_rank_promote_slot{index + 1}"]?.GetValue<int>();
            if (filled == 1)
                continue;
            if (filled != 0)
                return new(503, null, "Invalid material slot state.");
            var material = rule.Materials[index];
            if (material.ObjectType == 3)
                goldCost += material.Count;
            else if (material.ObjectType == 12 && material.ObjectId > 0)
            {
                var item = items.FirstOrDefault(entry => entry!["item_id"]!.GetValue<int>() == material.ObjectId);
                if (item is null || item["num"]!.GetValue<int>() < material.Count)
                    return new(409, null, "Insufficient material inventory.");
                item["num"] = item["num"]!.GetValue<int>() - material.Count;
            }
            else
                return new(503, null, "Unfilled material type not supported.");
        }

        var gold = updated["gold"]!.GetValue<int>();
        if (gold < goldCost)
            return new(409, null, "Insufficient gold.");
        updated["gold"] = gold - (int)goldCost;
        unit["rank"] = rule.NextRank;
        for (var slot = 1; slot <= 6; slot++)
            unit[$"is_set_rank_promote_slot{slot}"] = 0;
        PlayerState.Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["gold"] = updated["gold"]!.DeepClone(), ["unit"] = updated["unit"]!.DeepClone(), ["item"] = updated["item"]!.DeepClone() } });
    }

    internal LocalReply SetRankMaterial(SetRankMaterialCommand? request)
    {
        if (request?.EntityId is null or <= 0 || request.Value is null or < 1 or > 6)
            return new(400, null, "Invalid unit or material slot.");
        var rule = request.RankMaterial;
        if (rule is null || rule.UnitId <= 0 || rule.Rank <= 0 || rule.Count <= 0)
            return new(503, null, "Rank material definition unavailable.");
        if (rule.ObjectType is not (3 or 12) || request.Ids is { Length: > 0 })
            return new(503, null, "Material type not supported.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var unit = updated["unit"]!["unit_list"]!.AsArray().FirstOrDefault(entry => entry!["user_unit_id"]!.GetValue<long>() == request.EntityId);
        if (unit is null)
            return new(404, null, "Owned unit not found.");
        if (unit["unit_id"]!.GetValue<int>() != rule.UnitId || unit["rank"]!.GetValue<int>() != rule.Rank)
            return new(409, null, "Rank material definition is stale.");
        var field = $"is_set_rank_promote_slot{request.Value}";
        if (unit[field]?.GetValue<int>() != 0)
            return new(409, null, "Material slot unavailable or already filled.");
        var changed = new JsonObject();
        if (rule.ObjectType == 3)
        {
            var gold = updated["gold"]!.GetValue<int>();
            if (gold < rule.Count)
                return new(409, null, "Insufficient gold.");
            updated["gold"] = gold - rule.Count;
            changed["gold"] = gold - rule.Count;
        }
        else
        {
            if (rule.ObjectId <= 0)
                return new(503, null, "Material item unavailable.");
            var item = updated["item"]!["item_list"]!.AsArray().FirstOrDefault(entry => entry!["item_id"]!.GetValue<int>() == rule.ObjectId);
            if (item is null || item["num"]!.GetValue<int>() < rule.Count)
                return new(409, null, "Insufficient material inventory.");
            item["num"] = item["num"]!.GetValue<int>() - rule.Count;
            changed["item"] = updated["item"]!.DeepClone();
        }

        unit[field] = 1;
        changed["unit"] = updated["unit"]!.DeepClone();
        PlayerState.Save(updated);
        return new(0, new() { ["user_update"] = changed });
    }

    internal LocalReply ReleaseAutomaticNode(ReleaseAutomaticNodeCommand? request)
    {
        if (request?.EntityId is null or <= 0 || request.Value is null or <= 0)
            return new(400, null, "Unit and release IDs required.");
        var rule = request.AutomaticRelease;
        if (rule is null || rule.Id != request.Value || rule.UnitId <= 0 || rule.Level < 0 || rule.Stage < 0)
            return new(503, null, "Automatic release definition unavailable.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var unit = updated["unit"]!["unit_list"]!.AsArray().FirstOrDefault(entry => entry?["user_unit_id"]?.GetValue<long>() == request.EntityId);
        if (unit is null || unit["unit_id"]!.GetValue<int>() != rule.UnitId)
            return new(404, null, "Unit not found.");
        if (unit["release_functions"] is not JsonArray { Count: > 0 } released)
            return new(409, null, "Release progression has not started.");
        if (released.Any(id => id!.GetValue<int>() == rule.Id))
            return new(0, new() { ["user_update"] = new JsonObject { ["unit"] = updated["unit"]!.DeepClone() } });
        if (unit["level"]!.GetValue<int>() < rule.Level || (unit["reincarnation_stage"]?.GetValue<int>() ?? 0) < rule.Stage || rule.RequiresFullStats && !rule.FullStats)
            return new(409, null, "Release requirements not met.");
        released.Add(rule.Id);
        PlayerState.Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["unit"] = updated["unit"]!.DeepClone() } });
    }

    internal LocalReply UnlockExtraStat(UnlockExtraStatCommand? request)
    {
        if (request?.EntityId is null or <= 0 || request.Value is null or < 1 or > 6)
            return new(400, null, "Invalid unit or stat.");
        var rule = request.StatUnlock;
        if (rule is null || rule.CurrentStep < 0 || rule.CurrentStep == int.MaxValue || rule.NextStep != rule.CurrentStep + 1 || rule.Cost < 0 || rule.RequiredValue < 0)
            return new(503, null, "Stat unlock definition unavailable.");
        if (!rule.Enabled)
            return new(403, null, "Stat unlock is disabled.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var unit = updated["unit"]!["unit_list"]!.AsArray().FirstOrDefault(entry => entry!["user_unit_id"]!.GetValue<long>() == request.EntityId);
        if (unit is null)
            return new(404, null, "Owned unit not found.");
        var field = $"type_{request.Value}";
        if (unit["strong_fragrant_wood_limit_info"] is not JsonObject limits || unit["strong_fragrant_wood"] is not JsonObject stats)
            return new(503, null, "Extra stat state unavailable.");
        if (limits[field]?.GetValue<int>() != rule.CurrentStep || stats[field]?.GetValue<int>() != rule.RequiredValue)
            return new(409, null, "Stat must reach its current limit before unlocking.");
        var item = updated["item"]!["item_list"]!.AsArray().FirstOrDefault(entry => entry!["item_id"]!.GetValue<int>() == 410);
        if (rule.Cost > 0 && (item is null || item["num"]!.GetValue<int>() < rule.Cost))
            return new(409, null, "Insufficient unlock materials.");
        if (item is not null)
            item["num"] = item["num"]!.GetValue<int>() - rule.Cost;
        limits[field] = rule.NextStep;
        limits[BasicStatFields[request.Value.Value - 1][4..]] = rule.NextStep;
        limits["limitbreak_params"] = new JsonObject(Enumerable.Range(1, 6).Select(stat => new KeyValuePair<string, JsonNode?>(stat.ToString(), limits[$"type_{stat}"]!.DeepClone())));
        PlayerState.Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["unit"] = updated["unit"]!.DeepClone(), ["item"] = updated["item"]!.DeepClone() } });
    }

    internal static string[] BasicStatFields = ["add_life_point", "add_yang_attack", "add_yang_defense", "add_yin_attack", "add_yin_defense", "add_speed"];
    internal LocalReply StrengthenUnit(StrengthenUnitCommand? request, bool extra = false)
    {
        if (request?.EntityId is null or <= 0 || request.StatMaterials is not { Length: > 0 and <= 6 } materials || materials.Any(entry => entry.ItemId <= 0 || entry.Count <= 0) || materials.Select(entry => entry.ItemId).Distinct().Count() != materials.Length)
            return new(400, null, "Invalid strengthening materials.");
        if (materials.Any(entry => entry.Stat is < 1 or > 6 || entry.CostPerPoint <= 0 || entry.Current < 0 || entry.Maximum < entry.Current) || materials.Select(entry => entry.Stat).Distinct().Count() != materials.Length)
            return new(503, null, "Strengthening definition unavailable.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var unit = updated["unit"]!["unit_list"]!.AsArray().FirstOrDefault(entry => entry!["user_unit_id"]!.GetValue<long>() == request.EntityId);
        if (unit is null)
            return new(404, null, "Owned unit not found.");
        var stats = extra ? unit["strong_fragrant_wood"] as JsonObject : unit as JsonObject;
        if (stats is null)
            return new(503, null, "Strengthening state unavailable.");
        var items = updated["item"]!["item_list"]!.AsArray();
        foreach (var material in materials)
        {
            var field = extra ? $"type_{material.Stat}" : BasicStatFields[material.Stat - 1];
            if (extra && material.CostPerPoint != 1)
                return new(503, null, "Invalid extra strengthening cost.");
            if (stats[field]?.GetValue<int>() != material.Current)
                return new(409, null, "Strengthening state is stale.");
            if (material.Count % material.CostPerPoint != 0)
                return new(400, null, "Material count does not match point cost.");
            var gain = material.Count / material.CostPerPoint;
            if (gain > material.Maximum - material.Current)
                return new(409, null, "Strengthening exceeds current limit.");
            var item = items.FirstOrDefault(entry => entry!["item_id"]!.GetValue<int>() == material.ItemId);
            if (item is null || item["num"]!.GetValue<int>() < material.Count)
                return new(409, null, "Insufficient strengthening materials.");
            item["num"] = item["num"]!.GetValue<int>() - material.Count;
            stats[field] = material.Current + gain;
            if (extra)
                stats[BasicStatFields[material.Stat - 1]] = material.Current + gain;
        }

        PlayerState.Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["unit"] = updated["unit"]!.DeepClone(), ["item"] = updated["item"]!.DeepClone() } });
    }

    internal LocalReply ReceiveLectureTrust(ReceiveLectureTrustCommand? request)
    {
        if (request?.EntityId is null or <= 0)
            return new(400, null, "Invalid unit.");
        if (request.Value is null or <= 0)
            return new(503, null, "Trust limit unavailable.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var unit = updated["unit"]!["unit_list"]!.AsArray().FirstOrDefault(entry => entry!["user_unit_id"]!.GetValue<long>() == request.EntityId);
        if (unit is null)
            return new(404, null, "Owned unit not found.");
        var room = updated["lecture_room"]!;
        var slot = room["unit"]!["unit_list"]!.AsArray().FirstOrDefault(entry => entry!["user_unit_id"]!.GetValue<long>() == request.EntityId);
        if (slot is null || slot["put_trust_up_icon"]?.GetValue<int>() != 1)
            return new(409, null, "No pending trust reward.");
        var pending = updated["_local_lecture_trust_rewards"] as JsonArray;
        var reward = pending?.FirstOrDefault(entry => entry?["user_unit_id"]?.GetValue<long>() == request.EntityId);
        if (reward?["value"]?.GetValue<int>() is not > 0)
            return new(503, null, "Pending trust value unavailable.");
        var current = unit["trust"]!.GetValue<int>();
        var total = (long)current + reward["value"]!.GetValue<int>();
        if (current < 0 || total > request.Value)
            return new(409, null, "Trust capacity exceeded.");
        unit["trust"] = (int)total;
        slot["put_trust_up_icon"] = 0;
        pending!.Remove(reward);
        PlayerState.Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["unit"] = updated["unit"]!.DeepClone(), ["lecture_room"] = room.DeepClone() } });
    }

    internal LocalReply RecycleUnits(RecycleUnitsCommand? request)
    {
        if (request?.Ids is not { Length: > 0 and <= 10 } ids || ids.Any(id => id <= 0) || ids.Distinct().Count() != ids.Length)
            return new(400, null, "Invalid unit selection.");
        var rule = request.UnitRecycle;
        if (rule is null || !rule.Entries.Select(row => row.Id).Order().SequenceEqual(ids.Order()))
            return new(503, null, "Unit recycle definitions unavailable.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var units = updated["unit"]!["unit_list"]!.AsArray();
        var selected = ids.ToHashSet();
        if (units.Count <= ids.Length)
            return new(409, null, "At least one owned unit required.");
        foreach (var row in rule.Entries)
        {
            var unit = units.FirstOrDefault(entry => entry!["user_unit_id"]!.GetValue<long>() == row.Id);
            if (unit is null)
                return new(404, null, "Owned unit not found.");
            if (unit["is_protection"]?.GetValue<int>() != 0 || unit["is_lecture"]?.GetValue<int>() != 0)
                return new(409, null, "Unit is protected or occupied.");
            if (unit["unit_id"]!.GetValue<int>() != row.UnitId || unit["rare"]!.GetValue<int>() != row.Rare || unit["level_limitbreak"]!.GetValue<int>() != row.LimitBreak)
                return new(409, null, "Unit recycle definition is stale.");
            if (row.Gains.Any(gain => gain.Value < 0 || gain.Value > 0 && gain.Type is not (2 or 3 or 5 or 12)))
                return new(503, null, "Unit recycle reward unsupported.");
        }

        foreach (var section in new[]
        {
            "team",
            "home_unit_info",
            "favorite",
            "dispatch",
            "lecture_room",
            "training_room"
        }

        )
            if (ContainsUnitReference(updated[section], selected))
                return new(409, null, "Unit is assigned.");
        if (selected.Contains(updated["helper_user_unit_id1"]?.GetValue<long>() ?? 0) || ContainsUnitReference(updated["unit"]?["tower_unit_list"], selected) || updated["weekly_arena_user_unit_list"] is JsonArray weekly && weekly.Any(id => id is not null && selected.Contains(id.GetValue<long>())) || updated["_local_idle"]?["unit_slot_list"] is JsonArray slots && slots.Any(slot => selected.Contains(slot?["user_object_id"]?.GetValue<long>() ?? 0)))
            return new(409, null, "Unit is assigned.");
        var gains = new JsonArray();
        foreach (var group in rule.Entries.SelectMany(row => row.Gains).Where(gain => gain.Value > 0).GroupBy(gain => (gain.Type, gain.Id)))
        {
            var amount = group.Sum(gain => (long)gain.Value);
            if (amount > int.MaxValue)
                return new(409, null, "Recycle reward exceeds capacity.");
            if (group.Key.Type is 3 or 12)
            {
                var error = EconomyService.ApplyInventoryReward(updated, new(group.Key.Type, group.Key.Id, (int)amount, group.Key.Type == 3 ? rule.GoldMax : rule.ItemMax));
                if (error is not null)
                    return error;
            }
            else
            {
                if (group.Key.Id != 0)
                    return new(503, null, "Currency definition unavailable.");
                var points = group.Key.Type == 2;
                var owner = points ? updated["phantasm_point"]!.AsObject() : updated;
                var key = points ? "points" : "piece";
                var maximum = points ? rule.PointMax : int.MaxValue;
                var total = owner[key]!.GetValue<int>() + amount;
                if (maximum <= 0 || total > maximum)
                    return new(409, null, "Currency capacity exceeded.");
                owner[key] = (int)total;
            }

            gains.Add(new JsonObject { ["type"] = group.Key.Type, ["id"] = group.Key.Id, ["value"] = (int)amount });
        }

        updated["_local_unit_sequence"] = Math.Max(updated["_local_unit_sequence"]?.GetValue<long>() ?? 0, units.Select(entry => entry!["user_unit_id"]!.GetValue<long>()).Max());
        foreach (var picture in updated["picture"]!["picture_list"]!.AsArray())
            if (selected.Contains(picture!["equip_user_unit_id"]?.GetValue<long>() ?? 0))
                picture["equip_user_unit_id"] = 0L;
        foreach (var unit in units.Where(entry => selected.Contains(entry!["user_unit_id"]!.GetValue<long>())).ToArray())
            units.Remove(unit);
        PlayerState.Save(updated);
        var changes = new JsonObject();
        foreach (var key in new[]
        {
            "unit",
            "picture",
            "gold",
            "phantasm_point",
            "piece",
            "item"
        }

        )
            changes[key] = updated[key]!.DeepClone();
        return new(0, new() { ["gain_object_list"] = gains, ["user_update"] = changes, ["remove_user_unit_id_list"] = new JsonArray(ids.Select(id => (JsonNode?)JsonValue.Create(id)).ToArray()) });
    }

    internal static bool ContainsUnitReference(JsonNode? node, HashSet<long> selected)
    {
        if (node is JsonArray list)
            return list.Any(entry => ContainsUnitReference(entry, selected));
        if (node is not JsonObject map)
            return false;
        foreach (var pair in map)
        {
            if (pair.Key == "user_unit_id" && pair.Value is JsonValue value && value.TryGetValue<long>(out var id) && selected.Contains(id))
                return true;
            if (pair.Key is "user_unit_id_list" or "season_enhancement_user_unit_id_list" && pair.Value is JsonArray ids && ids.Any(id => id is not null && selected.Contains(id.GetValue<long>())))
                return true;
            if (ContainsUnitReference(pair.Value, selected))
                return true;
        }

        return false;
    }
}
