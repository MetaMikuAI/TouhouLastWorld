using System.Text.Json.Nodes;
using System.Text;
using System.Text.Json;
using System.Globalization;
using System.Security.Cryptography;

namespace TouhouLastWorld;

internal sealed class GachaService
{
    internal GachaService(EconomyService economyService, PlayerState playerState, SessionState sessionState)
    {
        EconomyService = economyService;
        PlayerState = playerState;
        SessionState = sessionState;
    }

    private readonly EconomyService EconomyService;
    private readonly PlayerState PlayerState;
    private readonly SessionState SessionState;
    internal JsonObject DrawGacha(int count)
    {
        // 本地单角色池；消耗对应实机主表常驻按钮 102、103。
        var updated = PlayerState.user.DeepClone().AsObject();
        var units = updated["unit"]!["unit_list"]!.AsArray();
        var unitId = PlayerState.unitTemplate["unit_id"]!.GetValue<int>();
        var nextId = EconomyService.LastUnitId + 1;
        var drops = new JsonArray();
        for (var index = 0; index < count; index++)
        {
            var isNew = units.All(unit => unit!["unit_id"]!.GetValue<int>() != unitId) ? 1 : 0;
            var unit = PlayerState.unitTemplate.DeepClone().AsObject();
            unit["user_unit_id"] = nextId + index;
            unit["is_new"] = isNew;
            units.Add(unit);
            drops.Add(new JsonObject { ["object_type"] = 9, ["object_id"] = unitId, ["is_new"] = isNew, ["unique_id"] = nextId + index });
        }

        var beforeCrystal = updated["crystal"]!.GetValue<int>();
        updated["crystal"] = beforeCrystal - count * 5;
        updated["gold"] = updated["gold"]!.GetValue<int>() - count * 1500;
        updated["_local_unit_sequence"] = nextId + count - 1;
        PlayerState.Save(updated);
        return new()
        {
            ["gacha_drop_list"] = drops,
            ["gacha_list"] = GachaButtons(),
            ["gacha_point_list"] = new JsonArray(),
            ["gacha_directing_no"] = 1,
            ["beforeUserPowder"] = PlayerState.user["fortune_powder"]!.DeepClone(),
            ["beforeUserOrb"] = beforeCrystal,
            ["user_update"] = new JsonObject
            {
                ["gold"] = PlayerState.user["gold"]!.DeepClone(),
                ["crystal"] = PlayerState.user["crystal"]!.DeepClone(),
                ["unit"] = PlayerState.user["unit"]!.DeepClone()
            }
        };
    }

    internal static JsonArray GachaButtons() => new(new JsonObject { ["gacha_button_id"] = 102, ["count"] = 0 }, new JsonObject { ["gacha_button_id"] = 103, ["count"] = 0 });
    internal LocalReply TutorialInventory(bool restore = false)
    {
        if (PlayerState.user["_local_first_draw_confirmed"]?.GetValue<bool>() != true)
            return new(409, null, "Confirmed initial inventory required.");
        return new(0, new() { [restore ? "user_update" : "user"] = PlayerState.user.DeepClone() });
    }

    internal int[] PendingIntroPictures => (PlayerState.user["_local_first_draw_pending"]?["picture_ids"] as JsonArray)?.Select(value => value!.GetValue<int>()).Distinct().ToArray() ?? Array.Empty<int>();

    internal LocalReply ConfirmFirstDraw(ConfirmFirstDrawCommand? request)
    {
        if (PlayerState.user["_local_first_draw_confirmed"]?.GetValue<bool>() == true)
            return FirstDrawReceipt();
        if (PlayerState.user["_local_first_draw_pending"] is not JsonObject pending || pending["unit_ids"] is not JsonArray { Count: 1 } selected || pending["picture_ids"] is not JsonArray pictureIds)
            return new(409, null, "Initial result required.");
        var rule = request?.IntroGrant;
        if (rule is null || rule.UnitCapacity <= 0 || rule.PictureCapacity < 0 || selected[0]?["unit_id"]?.GetValue<int>() != PlayerState.unitTemplate["unit_id"]!.GetValue<int>() || pictureIds.Any(id => rule.Pictures.Count(row => row.Id == id!.GetValue<int>()) != 1))
            return new(503, null, "Initial inventory definitions unavailable.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var units = updated["unit"]!["unit_list"]!.AsArray();
        var pictures = updated["picture"]!["picture_list"]!.AsArray();
        if (units.Count >= rule.UnitCapacity || (long)pictures.Count + pictureIds.Count > rule.PictureCapacity)
            return new(409, null, "Initial inventory capacity exceeded.");
        var unitSequence = Math.Max(updated["_local_unit_sequence"]?.GetValue<long>() ?? 0, units.Select(row => row!["user_unit_id"]!.GetValue<long>()).DefaultIfEmpty().Max());
        var pictureSequence = Math.Max(updated["_local_picture_sequence"]?.GetValue<long>() ?? 0, pictures.Select(row => row!["user_picture_id"]!.GetValue<long>()).DefaultIfEmpty().Max());
        if (unitSequence < 0 || unitSequence == long.MaxValue || pictureSequence < 0 || pictureSequence > long.MaxValue - pictureIds.Count)
            return new(409, null, "Inventory identifier limit exceeded.");
        var unit = PlayerState.unitTemplate.DeepClone().AsObject();
        unit["user_unit_id"] = unitSequence + 1;
        unit["is_new"] = 1;
        units.Add(unit);
        var albumUnits = updated["album"]!["unit_list"]!.AsArray();
        if (albumUnits.All(row => row!["unit_id"]!.GetValue<int>() != unit["unit_id"]!.GetValue<int>()))
            albumUnits.Add(new JsonObject { ["unit_id"] = unit["unit_id"]!.DeepClone(), ["talk_rank"] = 0, ["voice_set_id"] = selected[0]!["voice_set_id"]!.DeepClone(), ["character_episode_ids"] = new JsonArray(), ["home_motion_ids"] = new JsonArray() });
        var albumPictures = updated["album"]!["picture_list"]!.AsArray();
        foreach (var id in pictureIds)
        {
            var row = rule.Pictures.Single(row => row.Id == id!.GetValue<int>());
            pictures.Add(new JsonObject { ["user_picture_id"] = ++pictureSequence, ["picture_id"] = row.Id, ["spec_actualize"] = 0, ["potential_limitbreak"] = 0, ["equip_user_unit_id"] = 0L, ["is_protection"] = 0, ["is_new"] = 1, ["parameter1_value"] = row.Parameter1, ["parameter2_value"] = row.Parameter2, ["picture_characteristic_group_master_id"] = row.Characteristic });
            if (albumPictures.All(entry => entry!["picture_id"]!.GetValue<int>() != row.Id))
                albumPictures.Add(new JsonObject { ["picture_id"] = row.Id, ["potential_limitbreak"] = 0 });
        }

        updated["_local_unit_sequence"] = unitSequence + 1;
        updated["_local_picture_sequence"] = pictureSequence;
        updated["_local_first_draw_confirmed"] = true;
        PlayerState.Save(updated);
        return FirstDrawReceipt();
    }

    internal LocalReply FirstDrawReceipt() => new(0, new() { ["status"] = 0, ["unit_list"] = PlayerState.user["album"]!["unit_list"]!.DeepClone(), ["picture_list"] = PlayerState.user["album"]!["picture_list"]!.DeepClone(), ["user_update"] = new JsonObject { ["unit"] = PlayerState.user["unit"]!.DeepClone(), ["picture"] = PlayerState.user["picture"]!.DeepClone(), ["album"] = PlayerState.user["album"]!.DeepClone() } });
    internal LocalReply PrepareFirstDraw(PrepareFirstDrawCommand? request)
    {
        if (SessionState.account is null || string.IsNullOrEmpty(request?.Text) || request.Text != SessionState.account.PublicId)
            return new(403, null, "Local account mismatch.");
        if (PlayerState.user["_local_first_draw_confirmed"]?.GetValue<bool>() == true)
            return new(409, null, "Initial result already confirmed.");
        var source = PlayerState.user["_local_first_draw_pending"] ?? PlayerState.user["_local_first_draw_candidate"];
        var rules = request.IntroDefinitions;
        if (source is not JsonObject candidate || rules is null || candidate["unit_ids"] is not JsonArray { Count: 1 } units || candidate["picture_ids"] is not JsonArray pictures || pictures.Count > 9)
            return new(503, null, "Local initial result unavailable.");
        var unit = units[0];
        if (unit?["unit_id"]?.GetValue<int>() is not > 0 || !rules.UnitIds.Contains(unit["unit_id"]!.GetValue<int>()) || unit["voice_set_id"]?.GetValue<int>() is not > 0 || unit["voice_set_file_id"]?.GetValue<int>() is not > 0 || pictures.Any(id => id is null || !rules.PictureIds.Contains(id.GetValue<int>())))
            return new(503, null, "Initial result references unavailable definitions.");
        var response = new JsonObject
        {
            ["unit_ids"] = units.DeepClone(),
            ["picture_ids"] = pictures.DeepClone()
        };
        if (PlayerState.user["_local_first_draw_pending"] is null)
        {
            var updated = PlayerState.user.DeepClone().AsObject();
            updated["_local_first_draw_pending"] = response.DeepClone();
            PlayerState.Save(updated);
        }

        return new(0, response);
    }

    internal LocalReply ExchangeDrawPoints(LocalPointExchange? rule)
    {
        if (rule is null || rule.Id <= 0 || rule.PointId <= 0 || rule.Cost <= 0 || rule.Limit <= 0 || rule.ObjectId <= 0 || rule.Capacity <= 0 || rule.ObjectType is not (9 or 10) || (rule.ObjectType == 9 && rule.ObjectId != PlayerState.unitTemplate["unit_id"]!.GetValue<int>()) || (rule.ObjectType == 10 && rule.Picture?.Id != rule.ObjectId))
            return new(503, null, "Exchange definition unavailable.");
        if (!rule.Active)
            return new(409, null, "Exchange is not active.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var points = updated["_local_gacha_points"] as JsonArray ?? new JsonArray();
        var counts = updated["_local_gacha_exchange_counts"] as JsonArray ?? new JsonArray();
        var balances = points.Where(row => row?["gacha_point_id"]?.GetValue<int>() == rule.PointId).ToArray();
        var receipts = counts.Where(row => row?["gacha_point_exchange_master_id"]?.GetValue<int>() == rule.Id).ToArray();
        if (balances.Length > 1 || receipts.Length > 1)
            return new(503, null, "Duplicate exchange state.");
        var balance = balances.FirstOrDefault()?["value"]?.GetValue<int>() ?? 0;
        var used = receipts.FirstOrDefault()?["count"]?.GetValue<int>() ?? 0;
        if (balance < 0 || used < 0)
            return new(503, null, "Invalid exchange state.");
        if (balance < rule.Cost || used >= rule.Limit)
            return new(409, null, "Insufficient points or exchange limit reached.");
        var isUnit = rule.ObjectType == 9;
        var kind = isUnit ? "unit" : "picture";
        var listName = kind + "_list";
        var idName = "user_" + kind + "_id";
        var sequenceName = "_local_" + kind + "_sequence";
        var owned = updated[kind]![listName]!.AsArray();
        var sequence = Math.Max(updated[sequenceName]?.GetValue<long>() ?? 0, owned.Select(row => row![idName]!.GetValue<long>()).DefaultIfEmpty().Max());
        if (owned.Count >= rule.Capacity || sequence < 0 || sequence == long.MaxValue)
            return new(409, null, "Inventory capacity exceeded.");
        var uniqueId = sequence + 1;
        var album = updated["album"]![listName]!.AsArray();
        var isNew = album.All(row => row?[kind + "_id"]?.GetValue<int>() != rule.ObjectId) ? 1 : 0;
        JsonObject item;
        if (isUnit)
        {
            item = PlayerState.unitTemplate.DeepClone().AsObject();
            item[idName] = uniqueId;
            item["is_new"] = 1;
            if (isNew == 1)
                album.Add(new JsonObject { ["unit_id"] = rule.ObjectId, ["talk_rank"] = 0, ["voice_set_id"] = 0, ["character_episode_ids"] = new JsonArray(), ["home_motion_ids"] = new JsonArray() });
        }
        else
        {
            item = new JsonObject
            {
                [idName] = uniqueId,
                ["picture_id"] = rule.ObjectId,
                ["spec_actualize"] = 0,
                ["potential_limitbreak"] = 0,
                ["equip_user_unit_id"] = 0L,
                ["is_protection"] = 0,
                ["is_new"] = 1,
                ["parameter1_value"] = rule.Picture!.Parameter1,
                ["parameter2_value"] = rule.Picture.Parameter2,
                ["picture_characteristic_group_master_id"] = rule.Picture.Characteristic
            };
            if (isNew == 1)
                album.Add(new JsonObject { ["picture_id"] = rule.ObjectId, ["potential_limitbreak"] = 0 });
        }

        owned.Add(item);
        updated[sequenceName] = uniqueId;
        balances[0]!["value"] = balance - rule.Cost;
        if (receipts.Length == 0)
            counts.Add(new JsonObject { ["gacha_point_exchange_master_id"] = rule.Id, ["count"] = used + 1 });
        else
            receipts[0]!["count"] = used + 1;
        if (counts.Parent is null)
            updated["_local_gacha_exchange_counts"] = counts;
        PlayerState.Save(updated);
        return new(0, new() { ["gacha_point"] = balance - rule.Cost, ["remaining_count"] = rule.Limit - used - 1, ["gain_object"] = new JsonObject { ["object_type"] = rule.ObjectType, ["object_id"] = rule.ObjectId, ["unique_id"] = uniqueId, ["is_new"] = isNew }, ["user_update"] = new JsonObject { [kind] = updated[kind]!.DeepClone(), ["album"] = updated["album"]!.DeepClone() } });
    }

    internal LocalReply FixRetryDraw(FixRetryDrawCommand? request)
    {
        if (request?.Value is not > 0)
            return new(400, null, "Positive selection identifier required.");
        var entries = PlayerState.user["retry_gacha_count"]!.AsArray();
        var matches = entries.Where(row => row?["gacha_id"]?.GetValue<int>() == request.Value).ToArray();
        if (matches.Length != 1)
            return new(404, null, "Pending retry selection not found.");
        var entry = matches[0]!;
        var key = request.Value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var units = PlayerState.user["unit"]!["unit_list"]!.AsArray();
        if (entry["is_fix"]?.GetValue<int>() == 1)
        {
            var receipt = PlayerState.user["_local_retry_receipts"]?[key]?.GetValue<long>();
            return receipt is > 0 && units.Any(unit => unit!["user_unit_id"]!.GetValue<long>() == receipt) ? RetryReceipt(receipt.Value) : new(409, null, "Selection already confirmed without an available receipt.");
        }

        var rule = request.RetryFix;
        if (rule is null || rule.GachaId != request.Value || rule.UnitId != entry["object_id"]?.GetValue<int>() || rule.UnitId != PlayerState.unitTemplate["unit_id"]!.GetValue<int>() || rule.Capacity <= 0 || entry["object_type"]?.GetValue<int>() != 9 || entry["is_fix"]?.GetValue<int>() != 0)
            return new(503, null, "Retry selection definition unavailable.");
        if (!rule.Active)
            return new(1006, null, "Selection is no longer active.");
        if (units.Count >= rule.Capacity || EconomyService.LastUnitId == long.MaxValue)
            return new(409, null, "Unit capacity exceeded.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var uniqueId = EconomyService.LastUnitId + 1;
        var unit = PlayerState.unitTemplate.DeepClone().AsObject();
        unit["user_unit_id"] = uniqueId;
        unit["is_new"] = 1;
        updated["unit"]!["unit_list"]!.AsArray().Add(unit);
        var album = updated["album"]!["unit_list"]!.AsArray();
        if (album.All(row => row?["unit_id"]?.GetValue<int>() != rule.UnitId))
            album.Add(new JsonObject { ["unit_id"] = rule.UnitId, ["talk_rank"] = 0, ["voice_set_id"] = 0, ["character_episode_ids"] = new JsonArray(), ["home_motion_ids"] = new JsonArray() });
        updated["retry_gacha_count"]!.AsArray().Single(row => row!["gacha_id"]!.GetValue<int>() == request.Value)!["is_fix"] = 1;
        var receipts = updated["_local_retry_receipts"] as JsonObject ?? new JsonObject();
        if (receipts.Parent is null)
            updated["_local_retry_receipts"] = receipts;
        receipts[key] = uniqueId;
        updated["_local_unit_sequence"] = uniqueId;
        PlayerState.Save(updated);
        return RetryReceipt(uniqueId);
    }

    internal LocalReply RetryReceipt(long id) => new(0, new() { ["unique_id"] = id, ["user_update"] = new JsonObject { ["unit"] = PlayerState.user["unit"]!.DeepClone(), ["album"] = PlayerState.user["album"]!.DeepClone(), ["retry_gacha_count"] = PlayerState.user["retry_gacha_count"]!.DeepClone() } });
}
