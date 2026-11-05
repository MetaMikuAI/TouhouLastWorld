using System.Text.Json.Nodes;
using System.Text;
using System.Text.Json;
using System.Globalization;
using System.Security.Cryptography;

namespace TouhouLastWorld;

internal sealed class ShopService
{
    internal ShopService(EconomyService economyService, PlayerState playerState, SessionState sessionState)
    {
        EconomyService = economyService;
        PlayerState = playerState;
        SessionState = sessionState;
    }

    private readonly EconomyService EconomyService;
    private readonly PlayerState PlayerState;
    private readonly SessionState SessionState;
    internal static LocalReply CodeReply(int code) => new(0, new() { ["status"] = code == 0, ["code"] = code });
    internal LocalReply ExchangeLocalCode(ExchangeLocalCodeCommand? request)
    {
        var exchange = request?.CodeExchange;
        if (exchange is null || string.IsNullOrWhiteSpace(exchange.Code) || exchange.Code.Any(char.IsControl) || SessionState.account is null || exchange.UserId != SessionState.account.UserId || exchange.DeviceId != SessionState.account.DeviceId)
            return CodeReply(1002);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(exchange.Code)));
        var updated = PlayerState.user.DeepClone().AsObject();
        var entry = updated["_local_exchange_codes"]?[hash];
        if (entry is null)
            return CodeReply(1002);
        if (entry["received"]?.GetValue<bool>() == true)
            return CodeReply(1001);
        var group = entry["group"]?.GetValue<string>();
        if (!string.IsNullOrEmpty(group) && updated["_local_exchange_codes"]!.AsObject().Any(pair => pair.Value?["group"]?.GetValue<string>() == group && pair.Value?["received"]?.GetValue<bool>() == true))
            return CodeReply(1001);
        if (entry["rewards"] is not JsonArray { Count: > 0 } rewards || request?.MailRules is not { } rules)
            return CodeReply(1000);
        foreach (var reward in rewards)
        {
            var type = reward?["object_type"]?.GetValue<int>() ?? 0;
            var id = reward?["object_id"]?.GetValue<int>() ?? 0;
            var count = reward?["object_value"]?.GetValue<int>() ?? 0;
            var capacity = type switch
            {
                3 => rules.GoldMax,
                6 => rules.CrystalMax,
                12 when rules.ItemIds?.Contains(id) == true => rules.ItemMax,
                _ => 0
            };
            if (EconomyService.ApplyInventoryReward(updated, new(type, id, count, capacity)) is not null)
                return CodeReply(1000);
        }

        entry["received"] = true;
        PlayerState.Save(updated);
        var reply = CodeReply(0);
        reply.Body!["user_update"] = new JsonObject
        {
            ["gold"] = updated["gold"]!.DeepClone(),
            ["crystal"] = updated["crystal"]!.DeepClone(),
            ["item"] = updated["item"]!.DeepClone()
        };
        return reply;
    }

    internal LocalReply ExchangeItems(ExchangeItemsCommand? request)
    {
        if (request?.EntityId is null or <= 0 || request.Value is null or <= 0)
            return new(400, null, "Invalid exchange request.");
        var rule = request.ItemExchange;
        if (rule is null || rule.Id != request.EntityId || rule.SourceId <= 0 || rule.TargetId <= 0 || rule.SourceId == rule.TargetId || rule.Cost <= 0 || rule.Capacity <= 0)
            return new(503, null, "Item exchange definition unavailable.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var inventory = updated["item"]!["item_list"]!.AsArray();
        var source = inventory.FirstOrDefault(item => item!["item_id"]!.GetValue<int>() == rule.SourceId);
        var target = inventory.FirstOrDefault(item => item!["item_id"]!.GetValue<int>() == rule.TargetId);
        var cost = (long)rule.Cost * request.Value.Value;
        if (source is null || source["num"]!.GetValue<int>() < cost)
            return new(409, null, "Insufficient exchange materials.");
        var total = (long)(target?["num"]?.GetValue<int>() ?? 0) + request.Value.Value;
        if (total > rule.Capacity)
            return new(409, null, "Exchange exceeds item capacity.");
        source["num"] = source["num"]!.GetValue<int>() - (int)cost;
        if (target is null)
            inventory.Add(new JsonObject { ["item_id"] = rule.TargetId, ["num"] = (int)total });
        else
            target["num"] = (int)total;
        PlayerState.Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["item"] = updated["item"]!.DeepClone() } });
    }

    internal LocalReply ShowPassRewards(ShowPassRewardsCommand? request)
    {
        if (PlayerState.user["_local_pass_card"] is not JsonObject pass || pass["level"]?.GetValue<int>() is not (>= 1 and <= 80))
            return new(409, null, "Local pass progress unavailable.");
        var level = pass["level"]!.GetValue<int>();
        var rules = request?.PassRewards;
        if (rules is null || !rules.Any(row => row.Level == level) || !rules.Any(row => row.Level == (level + 9) / 10 * 10))
            return new(503, null, "Pass level definitions unavailable.");
        if (pass["had_got_free_reward_level"] is not JsonArray || pass["had_got_vip_reward_level"] is not JsonArray)
            return new(409, null, "Local reward records unavailable.");
        var response = QueryCatalog.EmptyPassCard();
        foreach (var field in pass)
            response[field.Key] = field.Value?.DeepClone();
        response["free_rewards"] = new JsonArray();
        response["vip_rewards"] = new JsonArray();
        return new(0, new() { ["user_pass_card"] = response });
    }

    internal LocalReply BuyPassGoods(BuyPassGoodsCommand? request)
    {
        if (request?.Value is null or <= 0)
            return new(400, null, "Positive goods ID required.");
        var rule = request.PassGoods;
        if (rule is null || rule.Id != request.Value || rule.Cost < 0 || rule.Limit <= 0)
            return new(503, null, "Goods definition unavailable.");
        var updated = PlayerState.user.DeepClone().AsObject();
        if (updated["_local_pass_card"] is not JsonObject pass || pass["pass_card_goods"] is not JsonArray goods)
            return new(409, null, "Local shop state unavailable.");
        var purchased = goods.FirstOrDefault(entry => entry?["goods_id"]?.GetValue<int>() == rule.Id);
        var count = purchased?["buy_times"]?.GetValue<int>() ?? 0;
        if (count < 0 || count >= rule.Limit)
            return new(409, null, "Goods purchase limit reached.");
        var coins = pass["coin"]?.GetValue<int>() ?? 0;
        if (coins < rule.Cost)
            return new(20010, null, "Insufficient local coins.");
        var error = EconomyService.ApplyInventoryReward(updated, rule.Reward);
        if (error is not null)
            return error;
        pass["coin"] = coins - rule.Cost;
        if (purchased is null)
            goods.Add(new JsonObject { ["goods_id"] = rule.Id, ["buy_times"] = 1 });
        else
            purchased["buy_times"] = count + 1;
        PlayerState.Save(updated);
        return new(0, new() { ["user_pass_card"] = pass.DeepClone(), ["user_update"] = new JsonObject { ["gold"] = updated["gold"]!.DeepClone(), ["crystal"] = updated["crystal"]!.DeepClone(), ["item"] = updated["item"]!.DeepClone() } });
    }

    internal LocalReply BuyPassLevels(BuyPassLevelsCommand? request, bool gift = false)
    {
        if (!gift && (request?.Value is null or <= 0 || request.Value % 20 != 0))
            return new(400, null, "Level purchase requires multiples of twenty.");
        var rules = request?.PassRewards;
        if (rules is null || rules.Length == 0 || rules.Any(row => row.Level is < 1 or > 80))
            return new(503, null, "Pass level definitions unavailable.");
        var updated = PlayerState.user.DeepClone().AsObject();
        if (updated["_local_pass_card"] is not JsonObject pass || pass["had_got_free_reward_level"] is not JsonArray freeReceived || pass["had_got_vip_reward_level"] is not JsonArray vipReceived)
            return new(409, null, "Local pass progress unavailable.");
        var current = pass["level"]?.GetValue<int>() ?? 0;
        if (gift && (pass["is_vip"]?.GetValue<int>() != 1 || pass["is_had_gift"]?.GetValue<int>() != 0))
            return new(409, null, "Level gift unavailable or already purchased.");
        var cost = gift ? 150 : request!.Value!.Value;
        var levels = gift ? Math.Min(20, 80 - current) : cost / 20;
        if (current is < 1 or >= 80 || levels > 80 - current)
            return new(409, null, "Pass level limit exceeded.");
        var orb = updated["orb"]!;
        var free = orb["free"]!.GetValue<long>();
        var purchased = orb["purchased"]!.GetValue<long>();
        if (free < 0 || purchased < 0 || (decimal)free + purchased < cost)
            return new(20000, null, "Insufficient local balance.");
        var freeSpent = Math.Min(free, cost);
        orb["free"] = free - freeSpent;
        orb["purchased"] = purchased - (cost - freeSpent);
        pass["level"] = current + levels;
        if (gift)
            pass["is_had_gift"] = 1;
        var vip = pass["is_vip"]?.GetValue<int>() == 1;
        pass["is_reward"] = rules.Any(row => row.Level <= current + levels && (row.Free.Length > 0 && !freeReceived.Any(id => id!.GetValue<int>() == row.Level) || vip && row.Vip.Length > 0 && !vipReceived.Any(id => id!.GetValue<int>() == row.Level))) ? 1 : 0;
        pass["free_rewards"] = new JsonArray();
        pass["vip_rewards"] = new JsonArray();
        PlayerState.Save(updated);
        return new(0, new() { ["user_pass_card"] = pass.DeepClone(), ["user_update"] = new JsonObject { ["orb"] = orb.DeepClone() } });
    }

    internal LocalReply PassOverview() => new(0, new() { ["user_pass_card"] = PlayerState.user["_local_pass_card"]?.DeepClone() ?? QueryCatalog.EmptyPassCard() });
    internal LocalReply CollectPassExperience(CollectPassExperienceCommand? request)
    {
        if (request?.Value is null or <= 0)
            return new(400, null, "Positive task ID required.");
        var rule = request.PassTask;
        if (rule is null || rule.Id != request.Value || rule.Experience <= 0 || rule.Levels.Length == 0 || rule.Levels.Concat(rule.VipLevels).Any(level => level is < 1 or > 80))
            return new(503, null, "Pass task definition unavailable.");
        var updated = PlayerState.user.DeepClone().AsObject();
        if (updated["_local_pass_card"] is not JsonObject pass || pass["tasks"] is not JsonArray tasks)
            return new(409, null, "Local pass progress unavailable.");
        var task = tasks.FirstOrDefault(entry => entry?["task_id"]?.GetValue<int>() == rule.Id);
        if (task?["can_cellable"]?.GetValue<int>() is not > 0)
            return new(409, null, "No completed task available to claim.");
        var level = pass["level"]?.GetValue<int>() ?? 0;
        var experience = pass["current_exp"]?.GetValue<int>() ?? -1;
        if (level is < 1 or > 80 || experience is < 0 or >= 150 || pass["had_got_free_reward_level"] is not JsonArray freeReceived || pass["had_got_vip_reward_level"] is not JsonArray vipReceived)
            return new(409, null, "Invalid local pass progress.");
        var total = (long)experience + rule.Experience;
        var next = (int)Math.Min(80L, level + total / 150);
        pass["level"] = next;
        pass["current_exp"] = next == 80 ? 0 : (int)(total % 150);
        task["can_cellable"] = task["can_cellable"]!.GetValue<int>() - 1;
        pass["is_task"] = tasks.Any(entry => entry?["can_cellable"]?.GetValue<int>() > 0) ? 1 : 0;
        var free = rule.Levels.Distinct().Where(value => value <= next && !freeReceived.Any(id => id!.GetValue<int>() == value)).Order().ToArray();
        var vip = pass["is_vip"]?.GetValue<int>() == 1 ? rule.VipLevels.Distinct().Where(value => value <= next && !vipReceived.Any(id => id!.GetValue<int>() == value)).Order().ToArray() : Array.Empty<int>();
        pass["free_rewards"] = new JsonArray();
        pass["vip_rewards"] = new JsonArray();
        pass["is_reward"] = free.Length > 0 || vip.Length > 0 ? 1 : 0;
        PlayerState.Save(updated);
        return new(0, new() { ["user_pass_card"] = pass.DeepClone() });
    }

    internal LocalReply CollectPassRewards(CollectPassRewardsCommand? request)
    {
        if (request?.Value is not (0 or 1) || request.Value == 0 && (request.EntityId is null or < 1 or > 80 || request.Text is not ("free" or "vip")))
            return new(400, null, "Invalid reward selection.");
        var rules = request.PassRewards;
        if (rules is null || rules.Length == 0 || rules.Any(row => row.Level is < 1 or > 80) || rules.Select(row => row.Level).Distinct().Count() != rules.Length)
            return new(503, null, "Pass rewards unavailable.");
        var updated = PlayerState.user.DeepClone().AsObject();
        if (updated["_local_pass_card"] is not JsonObject pass || pass["had_got_free_reward_level"] is not JsonArray freeReceived || pass["had_got_vip_reward_level"] is not JsonArray vipReceived)
            return new(409, null, "Local pass progress unavailable.");
        var level = pass["level"]?.GetValue<int>() ?? 0;
        var vip = pass["is_vip"]?.GetValue<int>() == 1;
        var freeClaims = new JsonArray();
        var vipClaims = new JsonArray();
        foreach (var row in rules.OrderBy(row => row.Level))
        {
            if (row.Level > level || request.Value == 0 && row.Level != request.EntityId)
                continue;
            foreach (var kind in new[]
            {
                "free",
                "vip"
            }

            )
            {
                if (request.Value == 0 && request.Text != kind || kind == "vip" && !vip)
                    continue;
                var received = kind == "free" ? freeReceived : vipReceived;
                var rewards = kind == "free" ? row.Free : row.Vip;
                if (rewards.Length == 0 || received.Any(id => id!.GetValue<int>() == row.Level))
                    continue;
                foreach (var reward in rewards)
                {
                    var error = EconomyService.ApplyInventoryReward(updated, reward);
                    if (error is not null)
                        return error;
                }

                received.Add(row.Level);
                (kind == "free" ? freeClaims : vipClaims).Add(row.Level);
            }
        }

        if (request.Value == 0 && freeClaims.Count + vipClaims.Count == 0)
            return new(409, null, "Reward unavailable or already received.");
        pass["is_reward"] = rules.Any(row => row.Level <= level && (row.Free.Length > 0 && !freeReceived.Any(id => id!.GetValue<int>() == row.Level) || vip && row.Vip.Length > 0 && !vipReceived.Any(id => id!.GetValue<int>() == row.Level))) ? 1 : 0;
        pass["free_rewards"] = new JsonArray();
        pass["vip_rewards"] = new JsonArray();
        if (freeClaims.Count + vipClaims.Count > 0)
            PlayerState.Save(updated);
        var response = pass.DeepClone().AsObject();
        response["free_rewards"] = freeClaims;
        response["vip_rewards"] = vipClaims;
        return new(0, new() { ["user_pass_card"] = response, ["user_update"] = new JsonObject { ["gold"] = updated["gold"]!.DeepClone(), ["crystal"] = updated["crystal"]!.DeepClone(), ["item"] = updated["item"]!.DeepClone() } });
    }

    internal LocalReply BuyPoints(LocalPointPurchase? rule)
    {
        if (rule is null || rule.Cost < 0 || rule.Base < 0 || rule.PerLevel < 0 || rule.Maximum <= 0)
            return new(503, null, "Point purchase configuration unavailable.");
        var points = PlayerState.user["phantasm_point"]!.AsObject();
        var count = points["purchase_count"]!.GetValue<int>();
        var level = PlayerState.user["level"]!.GetValue<int>();
        if (level <= 0 || rule.Level != level || count < 0 || count == int.MaxValue || rule.PurchaseCount != count)
            return new(409, null, "Point purchase state changed.");
        var amount = (long)rule.Base + (long)level * rule.PerLevel;
        var balance = points["points"]!.GetValue<int>();
        if (amount <= 0)
            return new(503, null, "Invalid point purchase amount.");
        if (balance < 0 || amount > (long)rule.Maximum - balance)
            return new(409, null, "Point capacity exceeded.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var orb = updated["orb"]!.AsObject();
        var free = orb["free"]!.GetValue<long>();
        var purchased = orb["purchased"]!.GetValue<long>();
        if (free < 0 || purchased < 0 || (decimal)free + purchased < rule.Cost)
            return new(409, null, "Insufficient local balance.");
        var freeSpent = Math.Min(free, rule.Cost);
        orb["free"] = free - freeSpent;
        orb["purchased"] = purchased - (rule.Cost - freeSpent);
        updated["phantasm_point"]!["points"] = (int)(balance + amount);
        updated["phantasm_point"]!["purchase_count"] = count + 1;
        PlayerState.Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["orb"] = orb.DeepClone(), ["phantasm_point"] = updated["phantasm_point"]!.DeepClone() } });
    }

    internal int GoldExchangeDefinitionId(int slotId)
    {
        var goods = (PlayerState.user["_local_shop_list"] as JsonArray)?.Where(shop => shop?["type"]?.GetValue<int>() == 3).SelectMany(shop => shop?["goods_list"] as JsonArray ?? new JsonArray()).Where(item => item?["shop_item_id"]?.GetValue<int>() == slotId).ToArray();
        return goods is { Length: 1 } ? goods[0]?["master_id"]?.GetValue<int>() ?? 0 : 0;
    }

    internal LocalReply ExchangeGoldGoods(ExchangeGoldGoodsCommand? request)
    {
        var rule = request?.GoldExchange;
        if (rule is null || rule.SlotId <= 0 || rule.MasterId <= 0 || rule.Cost < 0 || GoldExchangeDefinitionId(rule.SlotId) != rule.MasterId)
            return new(503, null, "Exchange definition unavailable.");
        if (rule.Count <= 0)
            return new(400, null, "Positive exchange quantity required.");
        if (!rule.Active)
            return new(1005, null, "Exchange is closed.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var shop = updated["_local_shop_list"]!.AsArray().First(entry => entry?["type"]?.GetValue<int>() == 3 && (entry["goods_list"] as JsonArray)?.Any(item => item?["shop_item_id"]?.GetValue<int>() == rule.SlotId) == true)!;
        var goods = shop["goods_list"]!.AsArray().First(item => item?["shop_item_id"]?.GetValue<int>() == rule.SlotId)!;
        var end = goods["end_at"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(end) && (!DateTimeOffset.TryParse(end, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var expires) || expires <= PlayerState.Clock.GetUtcNow()))
            return new(1005, null, "Exchange is closed.");
        var stock = goods["stock"]?.GetValue<int>() ?? 0;
        if (stock < rule.Count)
            return new(409, null, "Insufficient shop stock.");
        var cost = (long)rule.Count * rule.Cost;
        var gold = updated["gold"]!.GetValue<int>();
        if (cost > gold)
            return new(409, null, "Insufficient gold.");
        var amount = (long)rule.Reward.Count * rule.Count;
        if (amount is <= 0 or > int.MaxValue)
            return new(409, null, "Invalid reward quantity.");
        updated["gold"] = (int)(gold - cost);
        var error = EconomyService.ApplyInventoryReward(updated, rule.Reward with { Count = (int)amount });
        if (error is not null)
            return error;
        goods["stock"] = stock - rule.Count;
        PlayerState.Save(updated);
        return new(0, new() { ["update_shop"] = shop.DeepClone(), ["feature_product_shop_list"] = new JsonArray(), ["user_update"] = new JsonObject { ["gold"] = updated["gold"]!.DeepClone(), ["crystal"] = updated["crystal"]!.DeepClone(), ["item"] = updated["item"]!.DeepClone() } });
    }

    internal LocalReply RefreshExchangeShop(RefreshExchangeShopCommand? request)
    {
        var rule = request?.ShopRefresh;
        if (rule is null || rule.Costs.Length == 0 || rule.Costs.Any(cost => cost < 0))
            return new(503, null, "Refresh definitions unavailable.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var shops = (updated["_local_shop_list"] as JsonArray)?.Where(shop => shop?["type"]?.GetValue<int>() == 3).ToArray();
        if (shops is not { Length: 1 })
            return new(409, null, "Local exchange shop unavailable.");
        var shop = shops[0]!;
        var count = shop["manual_update_count"]?.GetValue<int>() ?? -1;
        if (count < 0 || count >= rule.Costs.Length)
            return new(409, null, "Refresh limit reached.");
        if (updated["_local_gold_shop_batches"] is not JsonArray { Count: > 0 } batches || batches[0] is not JsonArray { Count: > 0 } goods)
            return new(503, null, "Next local goods batch unavailable.");
        if (goods.Any(item => item?["master_id"]?.GetValue<int>() is not > 0 || !rule.ActiveGoods.Contains(item["master_id"]!.GetValue<int>()) || item["shop_item_id"]?.GetValue<int>() is not > 0 || item["stock"]?.GetValue<int>() is not >= 0) || goods.Select(item => item!["shop_item_id"]!.GetValue<int>()).Distinct().Count() != goods.Count)
            return new(503, null, "Invalid local goods batch.");
        var orb = updated["orb"]!;
        var free = orb["free"]!.GetValue<long>();
        var purchased = orb["purchased"]!.GetValue<long>();
        var cost = rule.Costs[count];
        if (free < 0 || purchased < 0 || (decimal)free + purchased < cost)
            return new(409, null, "Insufficient refresh balance.");
        var freeSpent = Math.Min(free, cost);
        orb["free"] = free - freeSpent;
        orb["purchased"] = purchased - (cost - freeSpent);
        shop["goods_list"] = goods.DeepClone();
        shop["manual_update_count"] = count + 1;
        batches.RemoveAt(0);
        PlayerState.Save(updated);
        return new(0, new() { ["exchange_shop"] = shop.DeepClone(), ["user_update"] = new JsonObject { ["orb"] = orb.DeepClone() } });
    }

    internal int FreeItemDefinitionId()
    {
        if (PlayerState.user["_local_shop_list"] is not JsonArray shops)
            return 0;
        var goods = shops.Where(shop => shop?["type"]?.GetValue<int>() == 10).SelectMany(shop => shop?["goods_list"] as JsonArray ?? new JsonArray()).ToArray();
        if (goods.Length != 1)
            return 0;
        var end = goods[0]?["end_at"]?.GetValue<string>();
        if (!string.IsNullOrEmpty(end) && (!DateTimeOffset.TryParse(end, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var expiry) || expiry <= PlayerState.Clock.GetUtcNow()))
            return 0;
        return goods[0]?["master_id"]?.GetValue<int>() ?? 0;
    }

    internal LocalReply GainFreeItem(GainFreeItemCommand? request)
    {
        var rule = request?.FreeItem;
        if (rule is null || rule.Id <= 0 || rule.Id != FreeItemDefinitionId())
            return new(503, null, "Free item definition unavailable.");
        var now = PlayerState.Clock.GetUtcNow();
        var last = PlayerState.user["last_free_item_gain_at"]?.GetValue<string>();
        if (!string.IsNullOrEmpty(last))
        {
            if (!DateTimeOffset.TryParse(last, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var received))
                return new(409, null, "Invalid local receipt time.");
            if (received.UtcDateTime.Date >= now.UtcDateTime.Date)
                return new(409, null, "Daily item already received.");
        }

        var updated = PlayerState.user.DeepClone().AsObject();
        var error = EconomyService.ApplyInventoryReward(updated, rule.Reward);
        if (error is not null)
            return error;
        updated["last_free_item_gain_at"] = now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        PlayerState.Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["gold"] = updated["gold"]!.DeepClone(), ["crystal"] = updated["crystal"]!.DeepClone(), ["item"] = updated["item"]!.DeepClone(), ["last_free_item_gain_at"] = updated["last_free_item_gain_at"]!.DeepClone() } });
    }

    internal LocalReply ExchangeTicket(ExchangeTicketCommand? request)
    {
        var rule = request?.TicketExchange;
        if (rule is null || rule.ExchangeId <= 0 || rule.TicketId <= 0 || rule.ItemId <= 0 || rule.ObjectId <= 0 || rule.Cost <= 0 || rule.Capacity <= 0 || rule.ObjectType is not (9 or 10) || (rule.ObjectType == 9 && rule.CostumeId <= 0))
            return new(503, null, "Exchange definition unavailable.");
        var isUnit = rule.ObjectType == 9;
        if (rule.Count <= 0 || (isUnit && rule.Count != 1))
            return new(400, null, "Invalid exchange quantity.");
        if (!rule.Active)
            return new(409, null, "Exchange is not active.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var kind = isUnit ? "unit" : "picture";
        var listName = kind + "_list";
        var objectIdName = kind + "_id";
        var idName = "user_" + objectIdName;
        var sequenceName = "_local_" + kind + "_sequence";
        var owned = updated[kind]![listName]!.AsArray();
        if ((long)owned.Count + rule.Count > rule.Capacity)
            return new(409, null, "Inventory capacity exceeded.");
        var item = updated["item"]!["item_list"]!.AsArray().FirstOrDefault(entry => entry?["item_id"]?.GetValue<int>() == rule.ItemId);
        var cost = (long)rule.Cost * rule.Count;
        if (item is null || item["num"]!.GetValue<int>() < cost)
            return new(409, null, "Insufficient exchange items.");
        var sequence = Math.Max(updated[sequenceName]?.GetValue<long>() ?? 0, owned.Select(entry => entry![idName]!.GetValue<long>()).DefaultIfEmpty().Max());
        if (sequence < 0 || sequence > long.MaxValue - rule.Count)
            return new(409, null, "Inventory identifier limit exceeded.");
        int isNew;
        if (isUnit)
        {
            isNew = EconomyService.AddOwnedUnit(updated, rule.ObjectId, rule.CostumeId, sequence + 1);
        }
        else
        {
            for (var index = 0; index < rule.Count; index++)
                owned.Add(new JsonObject { [idName] = sequence + index + 1, [objectIdName] = rule.ObjectId, ["spec_actualize"] = 0, ["potential_limitbreak"] = 0, ["equip_user_unit_id"] = 0L, ["is_protection"] = 0, ["is_new"] = 1, ["parameter1_value"] = rule.Parameter1, ["parameter2_value"] = rule.Parameter2, ["picture_characteristic_group_master_id"] = rule.Characteristic });
            var album = updated["album"]![listName]!.AsArray();
            isNew = album.All(entry => entry?[objectIdName]?.GetValue<int>() != rule.ObjectId) ? 1 : 0;
            if (isNew == 1)
                album.Add(new JsonObject { [objectIdName] = rule.ObjectId, ["potential_limitbreak"] = 0 });
            updated[sequenceName] = sequence + rule.Count;
        }

        item["num"] = (int)(item["num"]!.GetValue<int>() - cost);
        PlayerState.Save(updated);
        return new(0, new() { ["reward_object"] = new JsonObject { ["object_type"] = rule.ObjectType, ["object_id"] = rule.ObjectId, ["unique_id"] = sequence + 1, ["is_new"] = isNew }, ["user_update"] = new JsonObject { [kind] = updated[kind]!.DeepClone(), ["item"] = updated["item"]!.DeepClone(), ["album"] = updated["album"]!.DeepClone() } });
    }
}
