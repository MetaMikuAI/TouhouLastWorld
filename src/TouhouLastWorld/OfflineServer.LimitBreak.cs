using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalLimitBreak(int UnitId, int CurrentStep, int NextStep, int RequiredLevel,
    int GoldCost, int MaterialCount, int ItemId, int DestinyPerItem, int DestinyMaximum);

public sealed partial class OfflineServer
{
    private LocalReply LimitBreakUnit(LocalArguments? request)
    {
        if (request?.EntityId is null or <= 0 || request.Value is null or < 0)
            return new(400, null, "Invalid unit or material count.");
        if (request.Ids is { Length: > 0 }) return new(503, null, "Unit materials not supported.");
        var rule = request.LimitBreak;
        if (rule is null || rule.UnitId <= 0 || rule.CurrentStep < 0 || rule.CurrentStep == int.MaxValue
            || rule.NextStep != rule.CurrentStep + 1 || rule.RequiredLevel <= 0 || rule.GoldCost < 0
            || rule.MaterialCount <= 0 || rule.ItemId <= 0 || rule.DestinyPerItem < 0 || rule.DestinyMaximum <= 0)
            return new(503, null, "Limit break definition unavailable.");
        if (request.Value != rule.MaterialCount) return new(400, null, "Material count does not match definition.");
        var updated = user.DeepClone().AsObject();
        var unit = updated["unit"]!["unit_list"]!.AsArray()
            .FirstOrDefault(entry => entry!["user_unit_id"]!.GetValue<long>() == request.EntityId);
        if (unit is null) return new(404, null, "Owned unit not found.");
        if (unit["unit_id"]!.GetValue<int>() != rule.UnitId || unit["level_limitbreak"]!.GetValue<int>() != rule.CurrentStep)
            return new(409, null, "Limit break definition is stale.");
        if (unit["level"]!.GetValue<int>() < rule.RequiredLevel)
            return new(409, null, "Required unit level not reached.");
        var item = updated["item"]!["item_list"]!.AsArray()
            .FirstOrDefault(entry => entry!["item_id"]!.GetValue<int>() == rule.ItemId);
        var gold = updated["gold"]!.GetValue<int>();
        if (gold < rule.GoldCost || item is null || item["num"]!.GetValue<int>() < rule.MaterialCount)
            return new(409, null, "Insufficient gold or materials.");
        var destiny = unit["destiny_value"]!.GetValue<int>();
        if (destiny < 0 || destiny > rule.DestinyMaximum) return new(503, null, "Invalid destiny value.");
        unit["destiny_value"] = (int)Math.Clamp(destiny + (long)rule.MaterialCount * rule.DestinyPerItem, 1, rule.DestinyMaximum);
        unit["level_limitbreak"] = rule.NextStep;
        updated[UnitLimitBreakMilestone] = true;
        updated["gold"] = gold - rule.GoldCost;
        item["num"] = item["num"]!.GetValue<int>() - rule.MaterialCount;
        Save(updated);
        return new(0, new()
        {
            ["user_update"] = new JsonObject
            {
                ["gold"] = gold - rule.GoldCost, ["item"] = updated["item"]!.DeepClone(),
                ["unit"] = updated["unit"]!.DeepClone()
            }
        });
    }
}
