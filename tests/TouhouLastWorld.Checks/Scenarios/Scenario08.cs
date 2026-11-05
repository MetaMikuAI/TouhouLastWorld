using System.Text.Json.Nodes;
using TouhouLastWorld;

internal sealed partial class RegressionSuite
{
    private void Scenario08()
    {
        var productionDirectory = directory + "-facility-income";
        var productionServer = new OfflineServer(new JsonStateStore(productionDirectory), seed, "offline://resources/");
        productionServer.Dispatch("game_init");
        productionServer.Dispatch("login");
        var goldIncome = new LocalBoxIncome(1, 1200, 60, 900, 10_000);
        var spiritIncome = new LocalBoxIncome(1, 1200, 20, 300, 10_000);
        productionServer.Dispatch("home", arguments: new(GoldIncome: goldIncome, SpiritIncome: spiritIncome));
        var productionReload = new OfflineServer(new JsonStateStore(productionDirectory), seed, "offline://resources/");
        productionReload.Dispatch("game_init");
        productionReload.Dispatch("login");
        var facilityHome = productionReload.Dispatch("home", arguments: new(GoldIncome: goldIncome with { At = 11_200 }, SpiritIncome: spiritIncome with { At = 11_199 })).Body!;
        Check(facilityHome["gathering_gold"]!.GetValue<int>() == 60 && facilityHome["gathering_phantasm_point"]!.GetValue<int>() == 0, "facility income accrues after a full interval across relogin");
        var collected = productionReload.Dispatch("gather_phantasm_point", arguments: new(9999, 1, SpiritIncome: spiritIncome with { At = 11_200 })).Body!;
        Check(collected["gathering_phantasm_point"]!.GetValue<int>() == 0 && collected["user_update"]!["phantasm_point"]!["points"]!.GetValue<int>() == 120, "collection includes newly elapsed income");
        var fullGold = goldIncome with
        {
            At = 100_000
        };
        var stockAtCap = productionReload.Dispatch("home", arguments: new(GoldIncome: fullGold)).Body!;
        Check(stockAtCap["gathering_gold"]!.GetValue<int>() == 900, "facility income respects stock capacity");
        var collectedGold = productionReload.Dispatch("gather_gold", arguments: new(9999, 1, GoldIncome: fullGold)).Body!;
        Check(collectedGold["gathering_gold"]!.GetValue<int>() == 0 && productionReload.Dispatch("home", arguments: new(GoldIncome: fullGold with { At = 101_199 })).Body!["gathering_gold"]!.GetValue<int>() == 0, "time spent at capacity does not refill a collected box");
        Console.WriteLine("Passed 4 facility income checks.");
        var renovationSeed = JsonNode.Parse(seed)!;
        renovationSeed["gold_box"]!["renovation_finish_time"] = DateTimeOffset.UtcNow.AddHours(1).ToString("O");
        var earlyRenovation = new OfflineServer(new JsonStateStore(directory + "-early-renovation"), renovationSeed.ToJsonString(), "offline://resources/");
        earlyRenovation.Dispatch("game_init");
        earlyRenovation.Dispatch("login");
        Check(earlyRenovation.Dispatch("gold_box_renovation_end", arguments: new(2, 1)).Status == 409, "renovation cannot finish before its saved deadline");
        renovationSeed["gold_box"]!["renovation_finish_time"] = DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O");
        var renovationDirectory = directory + "-finished-renovation";
        var renovationServer = new OfflineServer(new JsonStateStore(renovationDirectory), renovationSeed.ToJsonString(), "offline://resources/");
        renovationServer.Dispatch("game_init");
        renovationServer.Dispatch("login");
        Check(renovationServer.Dispatch("gold_box_renovation_end", arguments: new(3, 1)).Status == 503, "renovation rejects missing or mismatched next-level data");
        var renovated = renovationServer.Dispatch("gold_box_renovation_end", arguments: new(2, 1)).Body!["user_update"]!["gold_box"]!;
        Check(renovated["level"]!.GetValue<int>() == 2 && renovated["renovation_finish_time"]!.GetValue<string>() == "" && renovationServer.Dispatch("login").Body!["user"]!["gold"]!.GetValue<int>() == 5000, "renovation advances exactly one level and clears its timer without a second charge");
        var renovationReload = new OfflineServer(new JsonStateStore(renovationDirectory), seed, "offline://resources/");
        renovationReload.Dispatch("game_init");
        renovationReload.Dispatch("login");
        Check(renovationReload.Dispatch("gold_box_renovation_end", arguments: new(3, 2)).Status == 409 && renovationReload.Dispatch("login").Body!["user"]!["gold_box"]!["level"]!.GetValue<int>() == 2, "completed renovation survives restart and cannot advance again");
        Console.WriteLine("Passed 4 renovation completion checks.");
        var startBoxDirectory = directory + "-start-box";
        var startBoxServer = new OfflineServer(new JsonStateStore(startBoxDirectory), seed, "offline://resources/");
        startBoxServer.Dispatch("game_init");
        startBoxServer.Dispatch("login");
        Check(startBoxServer.Dispatch("gold_box_renovation_start", arguments: new(Renovation: new(1, 2, 2, 10, 60))).Status == 409 && startBoxServer.Dispatch("gold_box_renovation_start", arguments: new(Renovation: new(1, 2, 1, 6000, 60))).Status == 409 && startBoxServer.Dispatch("login").Body!["user"]!["gold"]!.GetValue<int>() == 5000, "renovation start validates level and funds before charging");
        var startBoxReply = startBoxServer.Dispatch("gold_box_renovation_start", arguments: new(Renovation: new(1, 2, 1, 100, 3600))).Body!;
        Check(startBoxReply["user_update"]!["gold"]!.GetValue<int>() == 4900 && startBoxReply["user_update"]!["gold_box"]!["level"]!.GetValue<int>() == 1 && !string.IsNullOrEmpty(startBoxReply["user_update"]!["gold_box"]!["renovation_finish_time"]!.GetValue<string>()), "renovation start charges once and records a timer without advancing the level");
        var startBoxReload = new OfflineServer(new JsonStateStore(startBoxDirectory), seed, "offline://resources/");
        startBoxReload.Dispatch("game_init");
        startBoxReload.Dispatch("login");
        Check(startBoxReload.Dispatch("gold_box_renovation_start", arguments: new(Renovation: new(1, 2, 1, 100, 3600))).Status == 409 && startBoxReload.Dispatch("gold_box_renovation_end", arguments: new(2, 1)).Status == 409 && startBoxReload.Dispatch("login").Body!["user"]!["gold"]!.GetValue<int>() == 4900, "pending renovation survives restart without duplicate charge or early completion");
        var instantBox = new OfflineServer(new JsonStateStore(directory + "-instant-box"), seed, "offline://resources/");
        instantBox.Dispatch("game_init");
        instantBox.Dispatch("login");
        instantBox.Dispatch("gold_box_renovation_start", arguments: new(Renovation: new(1, 2, 1, 100, 0)));
        Check(instantBox.Dispatch("gold_box_renovation_end", arguments: new(2, 1)).Body!["user_update"]!["gold_box"]!["level"]!.GetValue<int>() == 2 && instantBox.Dispatch("login").Body!["user"]!["gold"]!.GetValue<int>() == 4900, "zero-duration configuration completes through the same start and end flow");
        Console.WriteLine("Passed 4 renovation start checks.");
        var pointRenovationSeed = JsonNode.Parse(seed)!;
        pointRenovationSeed["phantasm_box"]!["renovation_finish_time"] = DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O");
        pointRenovationSeed["gold_box"]!["renovation_finish_time"] = DateTimeOffset.UtcNow.AddHours(1).ToString("O");
        var pointRenovationDirectory = directory + "-point-renovation";
        var pointRenovationServer = new OfflineServer(new JsonStateStore(pointRenovationDirectory), pointRenovationSeed.ToJsonString(), "offline://resources/");
        pointRenovationServer.Dispatch("game_init");
        pointRenovationServer.Dispatch("login");
        Check(pointRenovationServer.Dispatch("phantasm_box_renovation_end", arguments: new(3, 1)).Status == 503, "point renovation requires matching next-level configuration");
        var pointRenovated = pointRenovationServer.Dispatch("phantasm_box_renovation_end", arguments: new(2, 1)).Body!;
        Check(pointRenovated["user_update"]!["phantasm_box"]!["level"]!.GetValue<int>() == 2 && pointRenovated["user_update"]!["phantasm_box"]!["renovation_finish_time"]!.GetValue<string>() == "" && pointRenovationServer.Dispatch("login").Body!["user"]!["gold_box"]!.ToJsonString() == pointRenovationSeed["gold_box"]!.ToJsonString(), "point renovation completes only the selected facility");
        var pointRenovationReload = new OfflineServer(new JsonStateStore(pointRenovationDirectory), seed, "offline://resources/");
        pointRenovationReload.Dispatch("game_init");
        pointRenovationReload.Dispatch("login");
        Check(pointRenovationReload.Dispatch("phantasm_box_renovation_end", arguments: new(3, 2)).Status == 409 && pointRenovationReload.Dispatch("login").Body!["user"]!["phantasm_box"]!["level"]!.GetValue<int>() == 2, "point renovation cannot be completed twice after restart");
        Console.WriteLine("Passed 3 point renovation completion checks.");
        var pointStartDirectory = directory + "-point-start";
        var pointStartServer = new OfflineServer(new JsonStateStore(pointStartDirectory), seed, "offline://resources/");
        pointStartServer.Dispatch("game_init");
        pointStartServer.Dispatch("login");
        Check(pointStartServer.Dispatch("phantasm_box_renovation_start", arguments: new(Renovation: new(1, 2, 2, 10, 60))).Status == 409 && pointStartServer.Dispatch("phantasm_box_renovation_start", arguments: new(Renovation: new(1, 2, 1, 6000, 60))).Status == 409, "point renovation checks account level and gold cost");
        var pointStarted = pointStartServer.Dispatch("phantasm_box_renovation_start", arguments: new(Renovation: new(1, 2, 1, 100, 0))).Body!;
        Check(pointStarted["user_update"]!["gold"]!.GetValue<int>() == 4900 && pointStarted["user_update"]!["phantasm_box"]!["level"]!.GetValue<int>() == 1 && pointStartServer.Dispatch("login").Body!["user"]!["phantasm_point"]!["points"]!.GetValue<int>() == 100, "point renovation consumes gold while preserving points and current level");
        var pointStartReload = new OfflineServer(new JsonStateStore(pointStartDirectory), seed, "offline://resources/");
        pointStartReload.Dispatch("game_init");
        pointStartReload.Dispatch("login");
        Check(pointStartReload.Dispatch("phantasm_box_renovation_start", arguments: new(Renovation: new(1, 2, 1, 100, 0))).Status == 409 && pointStartReload.Dispatch("phantasm_box_renovation_end", arguments: new(2, 1)).Body!["user_update"]!["phantasm_box"]!["level"]!.GetValue<int>() == 2 && pointStartReload.Dispatch("login").Body!["user"]!["gold"]!.GetValue<int>() == 4900, "point renovation timer persists and completes with only one charge");
        Console.WriteLine("Passed 3 point renovation start checks.");
        var pointBuySeed = JsonNode.Parse(seed)!;
        pointBuySeed["orb"]!["free"] = 3;
        pointBuySeed["orb"]!["purchased"] = 7;
        var pointBuyDirectory = directory + "-point-buy";
        var pointBuyServer = new OfflineServer(new JsonStateStore(pointBuyDirectory), pointBuySeed.ToJsonString(), "offline://resources/");
        pointBuyServer.Dispatch("game_init");
        pointBuyServer.Dispatch("login");
        Check(pointBuyServer.Dispatch("phantasm_point_buy").Status == 503 && pointBuyServer.Dispatch("phantasm_point_buy", arguments: new(PointPurchase: new(1, 1, 5, 10, 2, 999))).Status == 409, "point purchase requires current price and count");
        Check(pointBuyServer.Dispatch("phantasm_point_buy", arguments: new(PointPurchase: new(1, 0, 11, 10, 2, 999))).Status == 409 && pointBuyServer.Dispatch("phantasm_point_buy", arguments: new(PointPurchase: new(1, 0, 5, 10, 2, 110))).Status == 409 && pointBuyServer.Dispatch("login").Body!["user"]!["phantasm_point"]!["purchase_count"]!.GetValue<int>() == 0, "insufficient balance or capacity leaves purchase count unchanged");
        var pointBought = pointBuyServer.Dispatch("phantasm_point_buy", arguments: new(PointPurchase: new(1, 0, 5, 10, 2, 999))).Body!["user_update"]!;
        Check(pointBought["phantasm_point"]!["points"]!.GetValue<int>() == 112 && pointBought["phantasm_point"]!["purchase_count"]!.GetValue<int>() == 1 && pointBought["orb"]!["free"]!.GetValue<long>() == 0 && pointBought["orb"]!["purchased"]!.GetValue<long>() == 5, "point purchase applies the level formula and spends free balance first");
        var pointBuyReload = new OfflineServer(new JsonStateStore(pointBuyDirectory), seed, "offline://resources/");
        pointBuyReload.Dispatch("game_init");
        pointBuyReload.Dispatch("login");
        Check(pointBuyReload.Dispatch("phantasm_point_buy", arguments: new(PointPurchase: new(1, 0, 5, 10, 2, 999))).Status == 409 && pointBuyReload.Dispatch("login").Body!["user"]!["phantasm_point"]!["points"]!.GetValue<int>() == 112, "point purchase state survives restart and rejects stale count");
        Console.WriteLine("Passed 4 point purchase checks.");
        var rankMaterialSeed = JsonNode.Parse(seed)!;
        rankMaterialSeed["item"]!["item_list"]!.AsArray().Add(new JsonObject { ["item_id"] = 71, ["num"] = 5 });
        var rankMaterialUnit = rankMaterialSeed["unit"]!["unit_list"]![0]!;
        rankMaterialId = rankMaterialUnit["user_unit_id"]!.GetValue<long>();
        rankMaterialDefinition = new LocalRankMaterial(rankMaterialUnit["unit_id"]!.GetValue<int>(), 1, 12, 71, 3);
        var rankMaterialDirectory = directory + "-rank-material";
        var rankMaterialServer = new OfflineServer(new JsonStateStore(rankMaterialDirectory), rankMaterialSeed.ToJsonString(), "offline://resources/");
        rankMaterialServer.Dispatch("game_init");
        rankMaterialServer.Dispatch("login");
        Check(rankMaterialServer.Dispatch("unit_rank_promote_item_set", arguments: new(rankMaterialId, 7, RankMaterial: rankMaterialDefinition)).Status == 400 && rankMaterialServer.Dispatch("unit_rank_promote_item_set", arguments: new(rankMaterialId, 1, RankMaterial: rankMaterialDefinition with { Rank = 2 })).Status == 409 && rankMaterialServer.Dispatch("unit_rank_promote_item_set", arguments: new(rankMaterialId, 1, RankMaterial: rankMaterialDefinition with { ObjectType = 10 })).Status == 503, "rank material rejects invalid slots, stale ranks and unsupported types");
        Check(rankMaterialServer.Dispatch("unit_rank_promote_item_set", arguments: new(rankMaterialId, 1, RankMaterial: rankMaterialDefinition with { Count = 6 })).Status == 409 && rankMaterialServer.Dispatch("login").Body!["user"]!["unit"]!["unit_list"]![0]!["is_set_rank_promote_slot1"]!.GetValue<int>() == 0, "insufficient materials leave the slot unchanged");
        var rankMaterialReply = rankMaterialServer.Dispatch("unit_rank_promote_item_set", arguments: new(rankMaterialId, 1, RankMaterial: rankMaterialDefinition)).Body!["user_update"]!;
        Check(rankMaterialReply["item"]!["item_list"]![0]!["num"]!.GetValue<int>() == 2 && rankMaterialReply["unit"]!["unit_list"]![0]!["is_set_rank_promote_slot1"]!.GetValue<int>() == 1 && rankMaterialReply["unit"]!["unit_list"]![0]!["rank"]!.GetValue<int>() == 1, "rank material consumes inventory and fills only the selected slot");
        var rankMaterialReload = new OfflineServer(new JsonStateStore(rankMaterialDirectory), seed, "offline://resources/");
        rankMaterialReload.Dispatch("game_init");
        rankMaterialReload.Dispatch("login");
        Check(rankMaterialReload.Dispatch("unit_rank_promote_item_set", arguments: new(rankMaterialId, 1, RankMaterial: rankMaterialDefinition)).Status == 409 && rankMaterialReload.Dispatch("unit_rank_promote_item_set", arguments: new(rankMaterialId, 2, RankMaterial: rankMaterialDefinition with { ObjectType = 3, Count = 100 })).Body!["user_update"]!["gold"]!.GetValue<int>() == 4900 && rankMaterialReload.Dispatch("login").Body!["user"]!["item"]!["item_list"]![0]!["num"]!.GetValue<int>() == 2, "material slots persist and prevent duplicate charges while allowing another slot");
        Console.WriteLine("Passed 4 rank material checks.");
        var rankPromotionSeed = JsonNode.Parse(seed)!;
        rankPromotionSeed["item"]!["item_list"]!.AsArray().Add(new JsonObject { ["item_id"] = 71, ["num"] = 12 });
        rankPromotionSeed["unit"]!["unit_list"]![0]!["is_set_rank_promote_slot1"] = 1;
        var rankPromotionMaterials = Enumerable.Range(1, 6).Select(_ => rankMaterialDefinition).ToArray();
        rankPromotionMaterials[5] = rankMaterialDefinition with
        {
            ObjectType = 3,
            Count = 50
        };
        var rankPromotionRule = new LocalRankPromotion(rankMaterialDefinition.UnitId, 1, 2, 1, 100, rankPromotionMaterials);
        var rankPromotionDirectory = directory + "-rank-promotion";
        var rankPromotionServer = new OfflineServer(new JsonStateStore(rankPromotionDirectory), rankPromotionSeed.ToJsonString(), "offline://resources/");
        rankPromotionServer.Dispatch("game_init");
        rankPromotionServer.Dispatch("login");
        Check(rankPromotionServer.Dispatch("unit_rank_promote", arguments: new(rankMaterialId, RankPromotion: rankPromotionRule with { RequiredLevel = 999 })).Status == 409 && rankPromotionServer.Dispatch("unit_rank_promote", arguments: new(rankMaterialId, RankPromotion: rankPromotionRule with { GoldCost = 5000 })).Status == 409 && rankPromotionServer.Dispatch("login").Body!["user"]!["item"]!["item_list"]![0]!["num"]!.GetValue<int>() == 12, "promotion validates level and combined gold cost without partial material consumption");
        var rankPromotionTooMany = rankPromotionMaterials.ToArray();
        rankPromotionTooMany[4] = rankMaterialDefinition with
        {
            Count = 4
        };
        Check(rankPromotionServer.Dispatch("unit_rank_promote", arguments: new(rankMaterialId, RankPromotion: rankPromotionRule with { Materials = rankPromotionTooMany })).Status == 409 && rankPromotionServer.Dispatch("login").Body!["user"]!["unit"]!["unit_list"]![0]!["rank"]!.GetValue<int>() == 1, "duplicate material requirements are accumulated before saving");
        var rankPromoted = rankPromotionServer.Dispatch("unit_rank_promote", arguments: new(rankMaterialId, RankPromotion: rankPromotionRule)).Body!["user_update"]!;
        Check(rankPromoted["gold"]!.GetValue<int>() == 4850 && rankPromoted["item"]!["item_list"]![0]!["num"]!.GetValue<int>() == 0 && rankPromoted["unit"]!["unit_list"]![0]!["rank"]!.GetValue<int>() == 2 && Enumerable.Range(1, 6).All(slot => rankPromoted["unit"]!["unit_list"]![0]![$"is_set_rank_promote_slot{slot}"]!.GetValue<int>() == 0), "promotion skips paid slots, consumes remaining materials and resets slots for the next rank");
        var rankPromotionReload = new OfflineServer(new JsonStateStore(rankPromotionDirectory), seed, "offline://resources/");
        rankPromotionReload.Dispatch("game_init");
        rankPromotionReload.Dispatch("login");
        Check(rankPromotionReload.Dispatch("unit_rank_promote", arguments: new(rankMaterialId, RankPromotion: rankPromotionRule)).Status == 409 && rankPromotionReload.Dispatch("login").Body!["user"]!["gold"]!.GetValue<int>() == 4850, "promotion survives restart and rejects stale rank without charging again");
        Console.WriteLine("Passed 4 rank promotion checks.");
        var awakeningSeed = JsonNode.Parse(seed)!;
        awakeningSeed["piece"] = 100;
        var awakeningDirectory = directory + "-awakening";
        var awakeningServer = new OfflineServer(new JsonStateStore(awakeningDirectory), awakeningSeed.ToJsonString(), "offline://resources/");
        awakeningServer.Dispatch("game_init");
        awakeningServer.Dispatch("login");
        Check(awakeningServer.Dispatch("unit_rare_awaken", arguments: new(rankMaterialId, Awakening: new(1, 3, 20))).Status == 503 && awakeningServer.Dispatch("unit_rare_awaken", arguments: new(rankMaterialId, Awakening: new(1, 2, 101))).Status == 409 && awakeningServer.Dispatch("login").Body!["user"]!["piece"]!.GetValue<int>() == 100, "awakening validates next step and balance without charging");
        var awakened = awakeningServer.Dispatch("unit_rare_awaken", arguments: new(rankMaterialId, Awakening: new(1, 2, 20))).Body!["user_update"]!;
        Check(awakened["piece"]!.GetValue<int>() == 80 && awakened["unit"]!["unit_list"]![0]!["rare"]!.GetValue<int>() == 2 && awakened["unit"]!["unit_list"]![0]!["rank"]!.GetValue<int>() == 1, "awakening consumes configured pieces and changes only the selected growth stage");
        var awakeningReload = new OfflineServer(new JsonStateStore(awakeningDirectory), seed, "offline://resources/");
        awakeningReload.Dispatch("game_init");
        awakeningReload.Dispatch("login");
        Check(awakeningReload.Dispatch("unit_rare_awaken", arguments: new(rankMaterialId, Awakening: new(1, 2, 20))).Status == 409 && awakeningReload.Dispatch("login").Body!["user"]!["piece"]!.GetValue<int>() == 80, "awakening persists and rejects a stale retry");
        awakeningSeed["_local_idle"] = new JsonObject
        {
            ["unit_slot_list"] = new JsonArray(new JsonObject { ["user_object_id"] = rankMaterialId, ["content"] = 4 })
        };
        var awakeningBusy = new OfflineServer(new JsonStateStore(directory + "-awakening-busy"), awakeningSeed.ToJsonString(), "offline://resources/");
        awakeningBusy.Dispatch("game_init");
        awakeningBusy.Dispatch("login");
        Check(awakeningBusy.Dispatch("unit_rare_awaken", arguments: new(rankMaterialId, Awakening: new(1, 2, 20))).Status == 409 && awakeningBusy.Dispatch("login").Body!["user"]!["piece"]!.GetValue<int>() == 100, "awakening cannot overlap pending idle growth of the same kind");
        Console.WriteLine("Passed 4 awakening checks.");
    }
}
