using System.Text.Json.Nodes;
using System.Text;
using System.Text.Json;
using System.Globalization;
using System.Security.Cryptography;

namespace TouhouLastWorld;

internal sealed class EconomyService
{
    internal EconomyService(PlayerState playerState)
    {
        PlayerState = playerState;
    }

    private readonly PlayerState PlayerState;
    internal long LastUnitId => GetLastUnitId(PlayerState.user);

    internal static long GetLastUnitId(JsonObject state) => Math.Max(state["_local_unit_sequence"]?.GetValue<long>() ?? 0, state["unit"]!["unit_list"]!.AsArray().Select(unit => unit!["user_unit_id"]!.GetValue<long>()).DefaultIfEmpty().Max());
    internal int AddOwnedUnit(JsonObject state, int unitId, int costumeId, long uniqueId)
    {
        var unit = PlayerState.unitTemplate.DeepClone().AsObject();
        unit["user_unit_id"] = uniqueId;
        unit["unit_id"] = unitId;
        unit["equip_costume_id"] = costumeId;
        unit["is_new"] = 1;
        unit["last_motivation_at"] = PlayerState.Clock.GetUtcNow().ToString("O");
        state["unit"]!["unit_list"]!.AsArray().Add(unit);
        state["_local_unit_sequence"] = uniqueId;
        var album = state["album"]!["unit_list"]!.AsArray();
        if (album.Any(entry => entry?["unit_id"]?.GetValue<int>() == unitId))
            return 0;
        album.Add(new JsonObject { ["unit_id"] = unitId, ["talk_rank"] = 0, ["voice_set_id"] = 0, ["character_episode_ids"] = new JsonArray(), ["home_motion_ids"] = new JsonArray() });
        return 1;
    }
    internal static LocalReply? ApplyInventoryReward(JsonObject updated, LocalInventoryReward reward)
    {
        if (reward.Type is not (3 or 6 or 12) || reward.Count <= 0 || reward.Capacity <= 0)
            return new(503, null, "Reward definition unsupported.");
        if (reward.Type == 12)
        {
            if (reward.Id <= 0)
                return new(503, null, "Item definition unavailable.");
            var inventory = updated["item"]!["item_list"]!.AsArray();
            var item = inventory.FirstOrDefault(entry => entry?["item_id"]?.GetValue<int>() == reward.Id);
            var total = (long)(item?["num"]?.GetValue<int>() ?? 0) + reward.Count;
            if (total > reward.Capacity)
                return new(409, null, "Item reward exceeds capacity.");
            if (item is null)
                inventory.Add(new JsonObject { ["item_id"] = reward.Id, ["num"] = (int)total });
            else
                item["num"] = (int)total;
        }
        else
        {
            var key = reward.Type == 3 ? "gold" : "crystal";
            var total = (long)updated[key]!.GetValue<int>() + reward.Count;
            if (total > reward.Capacity)
                return new(409, null, "Currency reward exceeds capacity.");
            updated[key] = (int)total;
        }

        return null;
    }

}
