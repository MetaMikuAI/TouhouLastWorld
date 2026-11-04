using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalIntroDefinitions(int[] UnitIds, int[] PictureIds);
public sealed record LocalIntroPicture(int Id, int Parameter1, int Parameter2, int Characteristic);
public sealed record LocalIntroGrant(int UnitCapacity, int PictureCapacity, LocalIntroPicture[] Pictures);

public sealed partial class OfflineServer
{
    public int[] PendingIntroPictures => (user["_local_first_draw_pending"]?["picture_ids"] as JsonArray)?
        .Select(value => value!.GetValue<int>()).Distinct().ToArray() ?? Array.Empty<int>();

    private LocalReply ConfirmFirstDraw(LocalArguments? request)
    {
        if (user["_local_first_draw_confirmed"]?.GetValue<bool>() == true)
            return FirstDrawReceipt();
        if (user["_local_first_draw_pending"] is not JsonObject pending
            || pending["unit_ids"] is not JsonArray { Count: 1 } selected
            || pending["picture_ids"] is not JsonArray pictureIds)
            return new(409, null, "Initial result required.");
        var rule = request?.IntroGrant;
        if (rule is null || rule.UnitCapacity <= 0 || rule.PictureCapacity < 0
            || selected[0]?["unit_id"]?.GetValue<int>() != unitTemplate["unit_id"]!.GetValue<int>()
            || pictureIds.Any(id => rule.Pictures.Count(row => row.Id == id!.GetValue<int>()) != 1))
            return new(503, null, "Initial inventory definitions unavailable.");
        var updated = user.DeepClone().AsObject();
        var units = updated["unit"]!["unit_list"]!.AsArray();
        var pictures = updated["picture"]!["picture_list"]!.AsArray();
        if (units.Count >= rule.UnitCapacity || (long)pictures.Count + pictureIds.Count > rule.PictureCapacity)
            return new(409, null, "Initial inventory capacity exceeded.");
        var unitSequence = Math.Max(updated["_local_unit_sequence"]?.GetValue<long>() ?? 0,
            units.Select(row => row!["user_unit_id"]!.GetValue<long>()).DefaultIfEmpty().Max());
        var pictureSequence = Math.Max(updated["_local_picture_sequence"]?.GetValue<long>() ?? 0,
            pictures.Select(row => row!["user_picture_id"]!.GetValue<long>()).DefaultIfEmpty().Max());
        if (unitSequence < 0 || unitSequence == long.MaxValue || pictureSequence < 0 || pictureSequence > long.MaxValue - pictureIds.Count)
            return new(409, null, "Inventory identifier limit exceeded.");
        var unit = unitTemplate.DeepClone().AsObject();
        unit["user_unit_id"] = unitSequence + 1;
        unit["is_new"] = 1;
        units.Add(unit);
        var albumUnits = updated["album"]!["unit_list"]!.AsArray();
        if (albumUnits.All(row => row!["unit_id"]!.GetValue<int>() != unit["unit_id"]!.GetValue<int>()))
            albumUnits.Add(new JsonObject { ["unit_id"] = unit["unit_id"]!.DeepClone(), ["talk_rank"] = 0,
                ["voice_set_id"] = selected[0]!["voice_set_id"]!.DeepClone(),
                ["character_episode_ids"] = new JsonArray(), ["home_motion_ids"] = new JsonArray() });
        var albumPictures = updated["album"]!["picture_list"]!.AsArray();
        foreach (var id in pictureIds)
        {
            var row = rule.Pictures.Single(row => row.Id == id!.GetValue<int>());
            pictures.Add(new JsonObject { ["user_picture_id"] = ++pictureSequence, ["picture_id"] = row.Id,
                ["spec_actualize"] = 0, ["potential_limitbreak"] = 0, ["equip_user_unit_id"] = 0L,
                ["is_protection"] = 0, ["is_new"] = 1, ["parameter1_value"] = row.Parameter1,
                ["parameter2_value"] = row.Parameter2, ["picture_characteristic_group_master_id"] = row.Characteristic });
            if (albumPictures.All(entry => entry!["picture_id"]!.GetValue<int>() != row.Id))
                albumPictures.Add(new JsonObject { ["picture_id"] = row.Id, ["potential_limitbreak"] = 0 });
        }
        updated["_local_unit_sequence"] = unitSequence + 1;
        updated["_local_picture_sequence"] = pictureSequence;
        updated["_local_first_draw_confirmed"] = true;
        Save(updated);
        return FirstDrawReceipt();
    }

    private LocalReply FirstDrawReceipt() => new(0, new()
    {
        ["status"] = 0, ["unit_list"] = user["album"]!["unit_list"]!.DeepClone(),
        ["picture_list"] = user["album"]!["picture_list"]!.DeepClone(),
        ["user_update"] = new JsonObject { ["unit"] = user["unit"]!.DeepClone(),
            ["picture"] = user["picture"]!.DeepClone(), ["album"] = user["album"]!.DeepClone() }
    });

    private LocalReply PrepareFirstDraw(LocalArguments? request)
    {
        if (account is null || string.IsNullOrEmpty(request?.Text) || request.Text != account.PublicId)
            return new(403, null, "Local account mismatch.");
        if (user["_local_first_draw_confirmed"]?.GetValue<bool>() == true)
            return new(409, null, "Initial result already confirmed.");
        var source = user["_local_first_draw_pending"] ?? user["_local_first_draw_candidate"];
        var rules = request.IntroDefinitions;
        if (source is not JsonObject candidate || rules is null
            || candidate["unit_ids"] is not JsonArray { Count: 1 } units
            || candidate["picture_ids"] is not JsonArray pictures || pictures.Count > 9)
            return new(503, null, "Local initial result unavailable.");
        var unit = units[0];
        if (unit?["unit_id"]?.GetValue<int>() is not > 0 || !rules.UnitIds.Contains(unit["unit_id"]!.GetValue<int>())
            || unit["voice_set_id"]?.GetValue<int>() is not > 0 || unit["voice_set_file_id"]?.GetValue<int>() is not > 0
            || pictures.Any(id => id is null || !rules.PictureIds.Contains(id.GetValue<int>())))
            return new(503, null, "Initial result references unavailable definitions.");
        var response = new JsonObject { ["unit_ids"] = units.DeepClone(), ["picture_ids"] = pictures.DeepClone() };
        if (user["_local_first_draw_pending"] is null)
        {
            var updated = user.DeepClone().AsObject();
            updated["_local_first_draw_pending"] = response.DeepClone();
            Save(updated);
        }
        return new(0, response);
    }
}
