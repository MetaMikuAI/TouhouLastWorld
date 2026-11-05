using System;

using System.Collections;

using System.Globalization;


using BepInEx.Unity.IL2CPP.Utils.Collections;

using HarmonyLib;

using Il2CppInterop.Runtime;


using Applet;


using static TouhouLastWorld.Patches.HarmonyPrefixFlow;

using NativeRoutine = Il2CppSystem.Collections.IEnumerator;

using PacketCallback = Il2CppSystem.Action<int, string>;

using TextCallback = Il2CppSystem.Action<string>;

namespace TouhouLastWorld.Patches;

public static partial class NetworkRequestReader
{
    private static RequestDraft? ReadExchangeArguments(RequestPacket request)
    {
        var api = "exchange";
        RequestDraft? arguments = null;
        if (api == "exchange")
        {
            var change = request.TryCast<ExchangeRequest>();
            var master = MasterDB.This();
            var row = change?.type == 3 ? master?.GoldExchange?.Find(Plugin.Server.GoldExchangeDefinitionId(change.shop_item_id)) : null;
            var setting = master?.Setting;
            var time = GameSystem.This()?.Time;
            if (change != null && row != null && setting != null && time != null)
            {
                var capacity = row.object_type switch
                {
                    3 => setting.GOLD_MAX,
                    6 => setting.CRYSTAL_MAX,
                    12 when master?.Item?.Find(row.object_id) != null => setting.ITEM_MAX,
                    _ => 0
                };
                arguments = new RequestDraft(GoldExchange: new(change.shop_item_id, row.id, change.count, row.consume_gold,
                    string.IsNullOrWhiteSpace(row.end_at) || time.IsActiveDate(row.start_at, row.end_at),
                    new(row.object_type, row.object_id, row.object_value, capacity)));
            }
        }

        return arguments;
    }
    private static RequestDraft? ReadUpdateGoldExchangeShopListArguments(RequestPacket request)
    {
        var api = "update_gold_exchange_shop_list";
        RequestDraft? arguments = null;
        if (api == "update_gold_exchange_shop_list" && request.TryCast<UpdateGoldExchangeShopListRequest>() is not null)
        {
            var master = MasterDB.This();
            var costs = master?.GoldExchangeUpdate?.ToList()?.ToArray().OrderBy(row => row.id).ToArray();
            var goods = master?.GoldExchange?.ToList()?.ToArray();
            var time = GameSystem.This()?.Time;
            if (costs != null && goods != null && time != null
                && costs.Select((row, index) => row.id == index + 1).All(valid => valid))
                arguments = new RequestDraft(ShopRefresh: new(costs.Select(row => row.consume_orb).ToArray(),
                    goods.Where(row => string.IsNullOrWhiteSpace(row.end_at) || time.IsActiveDate(row.start_at, row.end_at)).Select(row => row.id).ToArray()));
        }

        return arguments;
    }
    private static RequestDraft? ReadVotivePictureExchangeArguments(RequestPacket request)
    {
        var api = "votive_picture_exchange";
        RequestDraft? arguments = null;
        if (api == "votive_picture_exchange") arguments = ReadTicketExchange(request.TryCast<VotivePictureExchangeRequest>());

        return arguments;
    }
    private static RequestDraft? ReadGainFreeItemArguments(RequestPacket request)
    {
        var api = "gain_free_item";
        RequestDraft? arguments = null;
        if (api == "gain_free_item" && request.TryCast<GainFreeItemRequest>() is not null)
        {
            var master = MasterDB.This();
            var row = master?.FreeItem?.Find(Plugin.Server.FreeItemDefinitionId());
            var setting = master?.Setting;
            if (row != null && setting != null)
            {
                var capacity = row.object_type switch
                {
                    3 => setting.GOLD_MAX,
                    6 => setting.CRYSTAL_MAX,
                    12 when master?.Item?.Find(row.object_id) != null => setting.ITEM_MAX,
                    _ => 0
                };
                arguments = new RequestDraft(FreeItem: new(row.id, new(row.object_type, row.object_id, row.object_value, capacity)));
            }
        }

        return arguments;
    }
    private static RequestDraft? ReadPassCardBuyGoodsArguments(RequestPacket request)
    {
        var api = "pass_card/buy_goods";
        RequestDraft? arguments = null;
        if (api == "pass_card/buy_goods")
        {
            var change = request.TryCast<BuyGoodsRequest>();
            var master = MasterDB.This();
            var row = change == null ? null : master?.PassCardShop?.Find(change.goods_id);
            var setting = master?.Setting;
            arguments = new RequestDraft(Value: change?.goods_id);
            if (row != null && setting != null)
            {
                var capacity = row.object_type switch
                {
                    3 => setting.GOLD_MAX,
                    6 => setting.CRYSTAL_MAX,
                    12 when master?.Item?.Find(row.item_id) != null => setting.ITEM_MAX,
                    _ => 0
                };
                arguments = arguments with
                {
                    PassGoods = new(row.id, row.consume_coin, row.limit_times,
                    new(row.object_type, row.item_id, row.value, capacity))
                };
            }
        }

        return arguments;
    }
    private static RequestDraft? ReadPassCardUserOrbAddLevelArguments(RequestPacket request)
    {
        var api = "pass_card/user_orb_add_level";
        RequestDraft? arguments = null;
        if (api == "pass_card/user_orb_add_level")
            arguments = ReadPassRewardDefinitions(new RequestDraft(Value: request.TryCast<AddLevelRequest>()?.orb));

        return arguments;
    }
    private static RequestDraft? ReadPassCardGetLevelGiftArguments(RequestPacket request)
    {
        var api = "pass_card/get_level_gift";
        RequestDraft? arguments = null;
        if (api == "pass_card/get_level_gift" && request.TryCast<LevelGiftRequest>() is not null)
            arguments = ReadPassRewardDefinitions(new RequestDraft());

        return arguments;
    }
    private static RequestDraft? ReadPassCardShowRewardArguments(RequestPacket request)
    {
        var api = "pass_card/show_reward";
        RequestDraft? arguments = null;
        if (api == "pass_card/show_reward" && request.TryCast<ShowRewardRequest>() is not null)
            arguments = ReadPassRewardDefinitions(new RequestDraft());

        return arguments;
    }
    private static RequestDraft? ReadPassCardCollectRewardArguments(RequestPacket request)
    {
        var api = "pass_card/collect_reward";
        RequestDraft? arguments = null;
        if (api == "pass_card/collect_reward") arguments = ReadPassRewards(request.TryCast<CollectRewardRequest>());

        return arguments;
    }
    private static RequestDraft? ReadPassCardCollectExpArguments(RequestPacket request)
    {
        var api = "pass_card/collect_exp";
        RequestDraft? arguments = null;
        if (api == "pass_card/collect_exp")
        {
            var change = request.TryCast<CollectExpRequest>();
            var master = MasterDB.This();
            var row = change == null ? null : master?.PassCardTask?.Find(change.task_id);
            var levels = master?.PassCard?.ToList()?.ToArray();
            arguments = new RequestDraft(Value: change?.task_id);
            if (row != null && levels != null && levels.All(entry => entry.id == entry.level))
                arguments = arguments with
                {
                    PassTask = new(row.id, row.exp,
                    levels.Where(entry => entry.GetFreeParam()?.free_data?.Count > 0).Select(entry => entry.level).ToArray(),
                    levels.Where(entry => entry.GetVipParam()?.pay_data?.Count > 0).Select(entry => entry.level).ToArray())
                };
        }

        return arguments;
    }
    private static RequestDraft? ReadGetExchangeCodeRewardArguments(RequestPacket request)
    {
        var api = "get_exchange_code_reward";
        RequestDraft? arguments = null;
        if (api == "get_exchange_code_reward")
        {
            var change = request.TryCast<ExchangeCodeRequest>();
            var master = MasterDB.This();
            var setting = master?.Setting;
            arguments = new RequestDraft(CodeExchange: change == null ? null : new(change.exchange_code, change.user_id, change.device_id),
                MailRules: setting == null ? null : new(setting.GOLD_MAX, setting.CRYSTAL_MAX, setting.ITEM_MAX,
                    master?.Item?.ToList()?.ToArray().Select(item => item.id).ToArray()));
        }

        return arguments;
    }
    private static RequestDraft? ReadItemExchangeArguments(RequestPacket request)
    {
        var api = "item_exchange";
        RequestDraft? arguments = null;
        if (api == "item_exchange")
        {
            var change = request.TryCast<ItemExchangeRequest>();
            var master = MasterDB.This();
            var row = change == null ? null : master?.ItemExchange?.ToList()?.ToArray().FirstOrDefault(entry => entry.id == change.item_exchange_id);
            var source = row == null ? null : master?.Item?.Find(row.consume_item_id);
            var target = row == null ? null : master?.Item?.Find(row.exchange_item_id);
            arguments = new RequestDraft(change?.item_exchange_id, change?.exchange_count);
            if (row != null && source != null && target != null && source.type == row.item_type && row.exchange_count == 1 && master?.Setting != null)
                arguments = arguments with { ItemExchange = new(row.id, row.consume_item_id, row.consume_count, row.exchange_item_id, master.Setting.ITEM_MAX) };
        }

        return arguments;
    }
    private static RequestDraft? ReadPhantasmPointBuyArguments(RequestPacket request)
    {
        var api = "phantasm_point_buy";
        RequestDraft? arguments = null;
        if (api == "phantasm_point_buy")
        {
            var nativeUser = User.This();
            var count = nativeUser?.phantasm_point?.purchase_count;
            var price = count is >= 0 and < int.MaxValue ? MasterDB.This()?.PhantasmPointShop?.Find(count.Value + 1) : null;
            var setting = MasterDB.This()?.Setting;
            arguments = new RequestDraft(PointPurchase: nativeUser == null || price == null || setting == null || !count.HasValue
                ? null : new(nativeUser.level, count.Value, price.consume_orb, setting.PHANTASM_POINT_BUY_BASE,
                    setting.PHANTASM_POINT_BUY_CALC_VALUE, setting.PHANTASM_POINT_MAX));
        }

        return arguments;
    }
}
