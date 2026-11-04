using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalPictureGrowth(int PictureId, int Current, int Next, int Maximum, int ItemId, int ItemCount, int GoldCost);

public sealed partial class OfflineServer
{
    private LocalReply StrengthenPicture(LocalArguments? request)
    {
        if (request?.EntityId is null or <= 0) return new(400, null, "Invalid equipment instance.");
        var rule = request.PictureGrowth;
        if (rule is null || rule.PictureId <= 0 || rule.Current < 0 || rule.Current == int.MaxValue
            || rule.Next != rule.Current + 1 || rule.Next > rule.Maximum || rule.ItemId <= 0 || rule.ItemCount <= 0 || rule.GoldCost < 0)
            return new(503, null, "Equipment growth definition unavailable.");
        var updated = user.DeepClone().AsObject();
        var picture = updated["picture"]!["picture_list"]!.AsArray()
            .FirstOrDefault(entry => entry!["user_picture_id"]!.GetValue<long>() == request.EntityId);
        if (picture is null) return new(404, null, "Owned equipment not found.");
        if (picture["picture_id"]!.GetValue<int>() != rule.PictureId || picture["spec_actualize"]!.GetValue<int>() != rule.Current)
            return new(409, null, "Equipment growth definition is stale.");
        var item = updated["item"]!["item_list"]!.AsArray()
            .FirstOrDefault(entry => entry!["item_id"]!.GetValue<int>() == rule.ItemId);
        var gold = updated["gold"]!.GetValue<int>();
        if (item is null || item["num"]!.GetValue<int>() < rule.ItemCount || gold < rule.GoldCost)
            return new(409, null, "Insufficient equipment growth materials.");
        item["num"] = item["num"]!.GetValue<int>() - rule.ItemCount;
        updated["gold"] = gold - rule.GoldCost;
        picture["spec_actualize"] = rule.Next;
        Save(updated);
        return new(0, new()
        {
            ["user_update"] = new JsonObject
            {
                ["picture"] = updated["picture"]!.DeepClone(), ["item"] = updated["item"]!.DeepClone(), ["gold"] = gold - rule.GoldCost
            }
        });
    }
}
