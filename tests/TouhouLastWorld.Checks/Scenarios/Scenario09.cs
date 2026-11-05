using System.Text.Json.Nodes;
using TouhouLastWorld;

internal sealed partial class RegressionSuite
{
    private void Scenario09()
    {
        var limitBreakSeed = JsonNode.Parse(seed)!;
        limitBreakSeed["item"]!["item_list"]!.AsArray().Add(new JsonObject { ["item_id"] = 71, ["num"] = 3 });
        limitBreakSeed["unit"]!["unit_list"]![0]!["destiny_value"] = 99;
        var limitBreakDirectory = directory + "-limit-break";
        var limitBreakServer = new OfflineServer(new JsonStateStore(limitBreakDirectory), limitBreakSeed.ToJsonString(), "offline://resources/");
        limitBreakServer.Dispatch("game_init");
        limitBreakServer.Dispatch("login");
        var limitBreakRule = new LocalLimitBreak(rankMaterialDefinition.UnitId, 0, 1, 1, 100, 2, 71, 5, 100);
        Check(limitBreakServer.Dispatch("unit_level_limitbreak", arguments: new(rankMaterialId, 1, LimitBreak: limitBreakRule)).Status == 400 && limitBreakServer.Dispatch("unit_level_limitbreak", arguments: new(rankMaterialId, 2, LimitBreak: limitBreakRule with { RequiredLevel = 999 })).Status == 409 && limitBreakServer.Dispatch("unit_level_limitbreak", arguments: new(rankMaterialId, 2, Ids: new long[] { 999 }, LimitBreak: limitBreakRule)).Status == 503, "limit break checks material count, level and unsupported unit materials");
        Check(limitBreakServer.Dispatch("unit_level_limitbreak", arguments: new(rankMaterialId, 2, LimitBreak: limitBreakRule with { GoldCost = 6000 })).Status == 409 && limitBreakServer.Dispatch("unit_level_limitbreak", arguments: new(rankMaterialId, 4, LimitBreak: limitBreakRule with { MaterialCount = 4 })).Status == 409 && limitBreakServer.Dispatch("login").Body!["user"]!["gold"]!.GetValue<int>() == 5000, "insufficient gold or items leaves balances unchanged");
        var limitBroken = limitBreakServer.Dispatch("unit_level_limitbreak", arguments: new(rankMaterialId, 2, LimitBreak: limitBreakRule)).Body!["user_update"]!;
        Check(limitBroken["gold"]!.GetValue<int>() == 4900 && limitBroken["item"]!["item_list"]![0]!["num"]!.GetValue<int>() == 1 && limitBroken["unit"]!["unit_list"]![0]!["level_limitbreak"]!.GetValue<int>() == 1 && limitBroken["unit"]!["unit_list"]![0]!["destiny_value"]!.GetValue<int>() == 100, "limit break consumes configured costs and caps destiny growth");
        var limitBreakReload = new OfflineServer(new JsonStateStore(limitBreakDirectory), seed, "offline://resources/");
        limitBreakReload.Dispatch("game_init");
        limitBreakReload.Dispatch("login");
        Check(limitBreakReload.Dispatch("unit_level_limitbreak", arguments: new(rankMaterialId, 2, LimitBreak: limitBreakRule)).Status == 409 && limitBreakReload.Dispatch("login").Body!["user"]!["gold"]!.GetValue<int>() == 4900, "limit break survives restart without accepting stale advancement");
        Console.WriteLine("Passed 4 limit break checks.");
        var statSeed = JsonNode.Parse(seed)!;
        statSeed["item"]!["item_list"] = new JsonArray(new JsonObject { ["item_id"] = 71, ["num"] = 10 }, new JsonObject { ["item_id"] = 72, ["num"] = 4 });
        var statDirectory = directory + "-stats";
        var statServer = new OfflineServer(new JsonStateStore(statDirectory), statSeed.ToJsonString(), "offline://resources/");
        statServer.Dispatch("game_init");
        statServer.Dispatch("login");
        var statRules = new LocalStatMaterial[]
        {
        new(71, 6, 1, 2, 0, 10),
        new(72, 4, 6, 2, 0, 10)
        };
        Check(statServer.Dispatch("unit_spec_actualize", arguments: new(rankMaterialId, StatMaterials: [statRules[0] with { Count = 3 }])).Status == 400 && statServer.Dispatch("unit_spec_actualize", arguments: new(rankMaterialId, StatMaterials: [statRules[0] with { Maximum = 2 }])).Status == 409, "strengthening requires whole points within the current limit");
        Check(statServer.Dispatch("unit_spec_actualize", arguments: new(rankMaterialId, StatMaterials: [statRules[0], statRules[1] with { Count = 6 }])).Status == 409 && statServer.Dispatch("login").Body!["user"]!["item"]!["item_list"]![0]!["num"]!.GetValue<int>() == 10, "a later material failure does not consume earlier materials");
        var statReply = statServer.Dispatch("unit_spec_actualize", arguments: new(rankMaterialId, StatMaterials: statRules)).Body!["user_update"]!;
        Check(statReply["unit"]!["unit_list"]![0]!["add_life_point"]!.GetValue<int>() == 3 && statReply["unit"]!["unit_list"]![0]!["add_speed"]!.GetValue<int>() == 2 && statReply["unit"]!["unit_list"]![0]!["add_yang_attack"]!.GetValue<int>() == 0 && statReply["item"]!["item_list"]![0]!["num"]!.GetValue<int>() == 4, "strengthening applies material costs to the selected stats only");
        var statReload = new OfflineServer(new JsonStateStore(statDirectory), seed, "offline://resources/");
        statReload.Dispatch("game_init");
        statReload.Dispatch("login");
        Check(statReload.Dispatch("unit_spec_actualize", arguments: new(rankMaterialId, StatMaterials: statRules)).Status == 409 && statReload.Dispatch("login").Body!["user"]!["unit"]!["unit_list"]![0]!["add_speed"]!.GetValue<int>() == 2, "strengthening persists and rejects stale stat snapshots");
        Console.WriteLine("Passed 4 strengthening checks.");
        var extraStatDirectory = directory + "-extra-stats";
        var extraStatServer = new OfflineServer(new JsonStateStore(extraStatDirectory), statSeed.ToJsonString(), "offline://resources/");
        extraStatServer.Dispatch("game_init");
        extraStatServer.Dispatch("login");
        var extraStatRules = new LocalStatMaterial[]
        {
        new(71, 6, 1, 1, 0, 10),
        new(72, 4, 6, 1, 0, 4)
        };
        Check(extraStatServer.Dispatch("unit_extra_spec_actualize", arguments: new(rankMaterialId, StatMaterials: statRules)).Status == 503 && extraStatServer.Dispatch("unit_extra_spec_actualize", arguments: new(rankMaterialId, StatMaterials: [extraStatRules[0], extraStatRules[1] with { Maximum = 3 }])).Status == 409 && extraStatServer.Dispatch("login").Body!["user"]!["item"]!["item_list"]![0]!["num"]!.GetValue<int>() == 10, "extra strengthening enforces its own cost and all current caps before saving");
        var extraStatReply = extraStatServer.Dispatch("unit_extra_spec_actualize", arguments: new(rankMaterialId, StatMaterials: extraStatRules)).Body!["user_update"]!;
        Check(extraStatReply["unit"]!["unit_list"]![0]!["strong_fragrant_wood"]!["type_1"]!.GetValue<int>() == 6 && extraStatReply["unit"]!["unit_list"]![0]!["strong_fragrant_wood"]!["type_6"]!.GetValue<int>() == 4 && extraStatReply["unit"]!["unit_list"]![0]!["strong_fragrant_wood"]!["add_life_point"]!.GetValue<int>() == 6 && extraStatReply["unit"]!["unit_list"]![0]!["add_life_point"]!.GetValue<int>() == 0 && extraStatReply["item"]!["item_list"]![0]!["num"]!.GetValue<int>() == 4, "extra strengthening updates serialized and derived extra values without changing base stats");
        var extraStatReload = new OfflineServer(new JsonStateStore(extraStatDirectory), seed, "offline://resources/");
        extraStatReload.Dispatch("game_init");
        extraStatReload.Dispatch("login");
        Check(extraStatReload.Dispatch("unit_extra_spec_actualize", arguments: new(rankMaterialId, StatMaterials: extraStatRules)).Status == 409 && extraStatReload.Dispatch("login").Body!["user"]!["unit"]!["unit_list"]![0]!["strong_fragrant_wood"]!["type_6"]!.GetValue<int>() == 4, "extra strengthening persists and rejects stale snapshots");
        Console.WriteLine("Passed 3 extra strengthening checks.");
        var unlockSeed = JsonNode.Parse(seed)!;
        unlockSeed["item"]!["item_list"]!.AsArray().Add(new JsonObject { ["item_id"] = 410, ["num"] = 5 });
        unlockSeed["unit"]!["unit_list"]![0]!["strong_fragrant_wood"]!["type_6"] = 10;
        var unlockDirectory = directory + "-stat-unlock";
        var unlockServer = new OfflineServer(new JsonStateStore(unlockDirectory), unlockSeed.ToJsonString(), "offline://resources/");
        unlockServer.Dispatch("game_init");
        unlockServer.Dispatch("login");
        Check(unlockServer.Dispatch("limitbreak_strong_fragrantwood", arguments: new(rankMaterialId, 6, StatUnlock: new(0, 1, 3, 10, false))).Status == 403 && unlockServer.Dispatch("limitbreak_strong_fragrantwood", arguments: new(rankMaterialId, 6, StatUnlock: new(0, 1, 3, 20, true))).Status == 409 && unlockServer.Dispatch("limitbreak_strong_fragrantwood", arguments: new(rankMaterialId, 6, StatUnlock: new(0, 1, 6, 10, true))).Status == 409, "stat unlock checks feature gate, current cap and materials");
        var unlocked = unlockServer.Dispatch("limitbreak_strong_fragrantwood", arguments: new(rankMaterialId, 6, StatUnlock: new(0, 1, 3, 10, true))).Body!["user_update"]!;
        Check(unlocked["item"]!["item_list"]![0]!["num"]!.GetValue<int>() == 2 && unlocked["unit"]!["unit_list"]![0]!["strong_fragrant_wood_limit_info"]!["type_6"]!.GetValue<int>() == 1 && unlocked["unit"]!["unit_list"]![0]!["strong_fragrant_wood_limit_info"]!["type_1"]!.GetValue<int>() == 0 && unlocked["unit"]!["unit_list"]![0]!["strong_fragrant_wood"]!["type_6"]!.GetValue<int>() == 10, "stat unlock charges once and changes only the selected limit stage");
        var unlockReload = new OfflineServer(new JsonStateStore(unlockDirectory), seed, "offline://resources/");
        unlockReload.Dispatch("game_init");
        unlockReload.Dispatch("login");
        Check(unlockReload.Dispatch("limitbreak_strong_fragrantwood", arguments: new(rankMaterialId, 6, StatUnlock: new(0, 1, 3, 10, true))).Status == 409 && unlockReload.Dispatch("login").Body!["user"]!["item"]!["item_list"]![0]!["num"]!.GetValue<int>() == 2, "stat unlock persists and rejects duplicate advancement");
        Console.WriteLine("Passed 3 stat unlock checks.");
        var pictureGrowthSeed = JsonNode.Parse(seed)!;
        pictureGrowthSeed["picture"]!["picture_list"] = new JsonArray(new JsonObject { ["user_picture_id"] = 700L, ["picture_id"] = 71, ["spec_actualize"] = 0, ["potential_limitbreak"] = 1, ["equip_user_unit_id"] = rankMaterialId });
        pictureGrowthSeed["item"]!["item_list"] = new JsonArray(new JsonObject { ["item_id"] = 72, ["num"] = 5 });
        var pictureGrowthDirectory = directory + "-picture-growth";
        var pictureGrowthServer = new OfflineServer(new JsonStateStore(pictureGrowthDirectory), pictureGrowthSeed.ToJsonString(), "offline://resources/");
        pictureGrowthServer.Dispatch("game_init");
        pictureGrowthServer.Dispatch("login");
        var pictureGrowthRule = new LocalPictureGrowth(71, 0, 1, 5, 72, 3, 100);
        Check(pictureGrowthServer.Dispatch("picture_spec_actualize", arguments: new(700, PictureGrowth: pictureGrowthRule with { Maximum = 0 })).Status == 503 && pictureGrowthServer.Dispatch("picture_spec_actualize", arguments: new(700, PictureGrowth: pictureGrowthRule with { ItemCount = 6 })).Status == 409 && pictureGrowthServer.Dispatch("picture_spec_actualize", arguments: new(700, PictureGrowth: pictureGrowthRule with { GoldCost = 6000 })).Status == 409, "equipment growth checks cap and both costs before saving");
        var pictureGrown = pictureGrowthServer.Dispatch("picture_spec_actualize", arguments: new(700, PictureGrowth: pictureGrowthRule)).Body!["user_update"]!;
        Check(pictureGrown["gold"]!.GetValue<int>() == 4900 && pictureGrown["item"]!["item_list"]![0]!["num"]!.GetValue<int>() == 2 && pictureGrown["picture"]!["picture_list"]![0]!["spec_actualize"]!.GetValue<int>() == 1 && pictureGrown["picture"]!["picture_list"]![0]!["potential_limitbreak"]!.GetValue<int>() == 1 && pictureGrown["picture"]!["picture_list"]![0]!["equip_user_unit_id"]!.GetValue<long>() == rankMaterialId, "equipment growth consumes configured costs and preserves assignment and other growth");
        var pictureGrowthReload = new OfflineServer(new JsonStateStore(pictureGrowthDirectory), seed, "offline://resources/");
        pictureGrowthReload.Dispatch("game_init");
        pictureGrowthReload.Dispatch("login");
        Check(pictureGrowthReload.Dispatch("picture_spec_actualize", arguments: new(700, PictureGrowth: pictureGrowthRule)).Status == 409 && pictureGrowthReload.Dispatch("login").Body!["user"]!["gold"]!.GetValue<int>() == 4900, "equipment growth persists and rejects stale advancement");
        Console.WriteLine("Passed 3 equipment growth checks.");
        var chemicalGrowthRule = pictureGrowthRule with
        {
            Current = 1,
            Next = 2,
            ItemCount = 2,
            GoldCost = 0
        };
        Check(pictureGrowthReload.Dispatch("picture_spec_actualize_chemicals", arguments: new(700, PictureGrowth: chemicalGrowthRule with { ItemCount = 3 })).Status == 409, "chemical growth rejects insufficient inventory");
        var chemicalGrown = pictureGrowthReload.Dispatch("picture_spec_actualize_chemicals", arguments: new(700, PictureGrowth: chemicalGrowthRule)).Body!["user_update"]!;
        Check(chemicalGrown["gold"]!.GetValue<int>() == 4900 && chemicalGrown["item"]!["item_list"]![0]!["num"]!.GetValue<int>() == 0 && chemicalGrown["picture"]!["picture_list"]![0]!["spec_actualize"]!.GetValue<int>() == 2, "chemical growth consumes only materials and advances equipment");
        Console.WriteLine("Passed 2 chemical growth checks.");
        var destinyGrowthSeed = JsonNode.Parse(seed)!;
        destinyGrowthSeed["unit"]!["unit_list"]![0]!["destiny_value"] = 80;
        destinyGrowthSeed["item"]!["item_list"] = new JsonArray(new JsonObject { ["item_id"] = 71, ["num"] = 5 }, new JsonObject { ["item_id"] = 72, ["num"] = 5 }, new JsonObject { ["item_id"] = 73, ["num"] = 5 });
        var destinyGrowthRule = new LocalDestinyGrowth(destinyGrowthSeed["unit"]!["unit_list"]![0]!["unit_id"]!.GetValue<int>(), 80, 100, [new(71, 2, 5), new(72, 3, 1), new(73, 1, 10)]);
        var destinyGrowthDirectory = directory + "-destiny-growth";
        var destinyGrowthServer = new OfflineServer(new JsonStateStore(destinyGrowthDirectory), destinyGrowthSeed.ToJsonString(), "offline://resources/");
        destinyGrowthServer.Dispatch("game_init");
        destinyGrowthServer.Dispatch("login");
        Check(destinyGrowthServer.Dispatch("unit_destiny_value_up", arguments: new(rankMaterialId, Ids: [123], DestinyGrowth: destinyGrowthRule)).Status == 503 && destinyGrowthServer.Dispatch("unit_destiny_value_up", arguments: new(rankMaterialId, DestinyGrowth: destinyGrowthRule with { Materials = [new(71, 0, 5), new(72, 0, 1), new(73, 0, 10)] })).Status == 400, "destiny growth rejects unsupported unit materials and empty selection");
        Check(destinyGrowthServer.Dispatch("unit_destiny_value_up", arguments: new(rankMaterialId, DestinyGrowth: destinyGrowthRule with { Materials = [new(71, 2, 5), new(72, 6, 1), new(73, 1, 10)] })).Status == 409 && destinyGrowthServer.Dispatch("login").Body!["user"]!["item"]!["item_list"]![0]!["num"]!.GetValue<int>() == 5, "destiny growth insufficient later material leaves earlier inventory unchanged");
        var destinyGrown = destinyGrowthServer.Dispatch("unit_destiny_value_up", arguments: new(rankMaterialId, DestinyGrowth: destinyGrowthRule)).Body!["user_update"]!;
        Check(destinyGrown["unit"]!["unit_list"]![0]!["destiny_value"]!.GetValue<int>() == 100 && destinyGrown["item"]!["item_list"]![0]!["num"]!.GetValue<int>() == 3 && destinyGrown["item"]!["item_list"]![1]!["num"]!.GetValue<int>() == 2 && destinyGrown["item"]!["item_list"]![2]!["num"]!.GetValue<int>() == 4, "destiny growth deducts all selected materials and clamps the combined gain");
        var destinyGrowthReload = new OfflineServer(new JsonStateStore(destinyGrowthDirectory), seed, "offline://resources/");
        destinyGrowthReload.Dispatch("game_init");
        destinyGrowthReload.Dispatch("login");
        Check(destinyGrowthReload.Dispatch("unit_destiny_value_up", arguments: new(rankMaterialId, DestinyGrowth: destinyGrowthRule)).Status == 409 && destinyGrowthReload.Dispatch("unit_destiny_value_up", arguments: new(rankMaterialId, DestinyGrowth: destinyGrowthRule with { Current = 100 })).Status == 409, "destiny growth persists and rejects stale or capped requests");
        destinyGrowthSeed["_local_idle"] = new JsonObject
        {
            ["unit_slot_list"] = new JsonArray(new JsonObject { ["user_object_id"] = rankMaterialId, ["content"] = 2 })
        };
        var busyDestinyServer = new OfflineServer(new JsonStateStore(directory + "-destiny-busy"), destinyGrowthSeed.ToJsonString(), "offline://resources/");
        busyDestinyServer.Dispatch("game_init");
        busyDestinyServer.Dispatch("login");
        Check(busyDestinyServer.Dispatch("unit_destiny_value_up", arguments: new(rankMaterialId, DestinyGrowth: destinyGrowthRule)).Status == 409, "destiny growth rejects active background growth");
        Console.WriteLine("Passed 5 destiny growth checks.");
        pictureBreakSeed = JsonNode.Parse(seed)!;
        pictureBreakSeed["picture"]!["picture_list"] = new JsonArray(new JsonObject { ["user_picture_id"] = 700L, ["picture_id"] = 71, ["potential_limitbreak"] = 0, ["spec_actualize"] = 2, ["is_protection"] = 1 }, new JsonObject { ["user_picture_id"] = 701L, ["picture_id"] = 71, ["potential_limitbreak"] = 1, ["is_protection"] = 0, ["equip_user_unit_id"] = rankMaterialId }, new JsonObject { ["user_picture_id"] = 702L, ["picture_id"] = 71, ["potential_limitbreak"] = 0, ["is_protection"] = 0 });
        pictureBreakSeed["unit"]!["unit_list"]![0]!["equip_spellcard1_user_picture_id"] = 701L;
        var pictureBreakRule = new LocalPictureLimitBreak(71, 0, 4, [100, 200]);
        var pictureBreakDirectory = directory + "-picture-break";
        var pictureBreakServer = new OfflineServer(new JsonStateStore(pictureBreakDirectory), pictureBreakSeed.ToJsonString(), "offline://resources/");
        pictureBreakServer.Dispatch("game_init");
        pictureBreakServer.Dispatch("login");
        Check(pictureBreakServer.Dispatch("picture_potential_limitbreak", arguments: new(700, Ids: [701, 701], PictureLimitBreak: pictureBreakRule)).Status == 400 && pictureBreakServer.Dispatch("picture_potential_limitbreak", arguments: new(700, Ids: [701, 702], PictureLimitBreak: pictureBreakRule with { Maximum = 1 })).Status == 409 && pictureBreakServer.Dispatch("picture_potential_limitbreak", arguments: new(700, Ids: [701, 702], PictureLimitBreak: pictureBreakRule with { GoldCosts = [5000, 1] })).Status == 409, "equipment limit break rejects duplicate materials, excessive steps and insufficient gold");
        var pictureBroken = pictureBreakServer.Dispatch("picture_potential_limitbreak", arguments: new(700, Ids: [701, 702], PictureLimitBreak: pictureBreakRule)).Body!;
        Check(pictureBroken["remove_user_picture_id_list"]!.AsArray().Select(id => id!.GetValue<long>()).SequenceEqual([701L, 702L]) && pictureBroken["user_update"]!["picture"]!["picture_list"]!.AsArray().Count == 1 && pictureBroken["user_update"]!["picture"]!["picture_list"]![0]!["potential_limitbreak"]!.GetValue<int>() == 2 && pictureBroken["user_update"]!["picture"]!["picture_list"]![0]!["spec_actualize"]!.GetValue<int>() == 2 && pictureBroken["user_update"]!["unit"]!["unit_list"]![0]!["equip_spellcard1_user_picture_id"]!.GetValue<long>() == 0 && pictureBroken["user_update"]!["gold"]!.GetValue<int>() == 4700, "equipment limit break advances per material, clears assignments and emits native removal list");
        var pictureBreakReload = new OfflineServer(new JsonStateStore(pictureBreakDirectory), seed, "offline://resources/");
        pictureBreakReload.Dispatch("game_init");
        pictureBreakReload.Dispatch("login");
        Check(pictureBreakReload.Dispatch("picture_potential_limitbreak", arguments: new(700, Ids: [701, 702], PictureLimitBreak: pictureBreakRule)).Status == 409 && pictureBreakReload.Dispatch("login").Body!["user"]!["picture"]!["picture_list"]!.AsArray().Count == 1, "equipment consumption persists and stale request does not repeat costs");
        pictureBreakSeed["picture"]!["picture_list"]![2]!["is_protection"] = 1;
        var protectedPictureServer = new OfflineServer(new JsonStateStore(directory + "-picture-break-protected"), pictureBreakSeed.ToJsonString(), "offline://resources/");
        protectedPictureServer.Dispatch("game_init");
        protectedPictureServer.Dispatch("login");
        Check(protectedPictureServer.Dispatch("picture_potential_limitbreak", arguments: new(700, Ids: [701, 702], PictureLimitBreak: pictureBreakRule)).Status == 409 && protectedPictureServer.Dispatch("login").Body!["user"]!["picture"]!["picture_list"]!.AsArray().Count == 3, "protected material prevents all consumption");
        Console.WriteLine("Passed 4 equipment limit break checks.");
    }
}
