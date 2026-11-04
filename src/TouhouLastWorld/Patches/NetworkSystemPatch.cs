using System;
using System.Collections;
using System.Globalization;

using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;

using Applet;

using static TouhouLastWorld.Patches.HarmonyPrefixFlow;
using NativeRoutine = Il2CppSystem.Collections.IEnumerator;
using PacketCallback = Il2CppSystem.Action<int, string>;
using TextCallback = Il2CppSystem.Action<string>;

namespace TouhouLastWorld.Patches;

[HarmonyPatch(typeof(Applet.NetworkSystem))]
[AutoLog]
public partial class NetworkSystemPatch
{
    [HarmonyPatch(nameof(NetworkSystem.TransceiveRoutine), typeof(RequestPacket), typeof(ResponsePacket), typeof(PacketCallback))]
    [HarmonyPrefix]
    public static bool TransceiveRoutine_Prefix(NetworkSystem __instance, RequestPacket request,
        ResponsePacket response, PacketCallback action, ref NativeRoutine __result)
    {
        __result = Respond(__instance, request, response, action).WrapToIl2Cpp();
        return SkipOriginal;
    }

    private static IEnumerator Respond(NetworkSystem network, RequestPacket request, ResponsePacket response, PacketCallback action)
    {
        yield return null;
        var api = request.GetAPIName();
        var gacha = api == "gacha_do" ? request.TryCast<GachaDoRequest>() : null;
        var battle = api == "battle_end" ? request.TryCast<BattleEndRequest>() : null;
        var protection = api == "unit_protection" ? request.TryCast<UnitProtectionRequest>() : null;
        var arguments = protection == null ? null : new LocalArguments(protection.user_unit_id, protection.is_protection);
        if (api == "unit_recycle") arguments = ReadUnitRecycle(request.TryCast<UnitRecycleRequest>());
        if (api == "get_clear_history_playerinfo") arguments = ReadHistoryPlayer(request.TryCast<ClearHistoryPlayerInfoRequest>()?.player_id);
        if (api == "friend_get_history") arguments = ReadHistoryPlayer(request.TryCast<FriendGetHistoryRequest>()?.player_id);
        if (api == "tower_unit_recovery") arguments = new(Text: request.TryCast<TowerUnitRecoveryRequest>()?.tower_unit_info);
        if (api == "mission_end") arguments = ReadMissionClaim(request.TryCast<MissionEndRequest>());
        if (api == "battle_pass_mission_end") arguments = ReadBattlePassClaim(request.TryCast<BattlePassMissionEndRequest>());
        if (api == "battle_pass_receive_reward") arguments = ReadBattlePassRewards();
        if (api == "get_series_battle_ranking_teaminfo") arguments = ReadRankingHistory(request.TryCast<SeriesBattleRankingGetTeamInfoRequest>());
        if (api == "mini_game_top") arguments = new(Ids: MasterDB.This()?.MiniGame?.GetAll()?.ToArray().Select(row => (long)row.id).ToArray());
        if (api == "dispatch_end") arguments = ReadDispatchSettlement(request.TryCast<DispatchEndRequest>());
        if (api == "gacha_point_exchange") arguments = ReadPointExchange(request.TryCast<GachaPointExchangeRequest>());
        if (api == "retry_gacha_fix")
        {
            var change = request.TryCast<GachaRetryFixRequest>();
            if (change != null)
            {
                arguments = new LocalArguments(Value: change.gacha_id);
                var master = MasterDB.This();
                var current = User.This();
                var row = master?.Gacha?.Find(change.gacha_id);
                var selected = current?.FindRetryGachaData(change.gacha_id);
                var capacity = current?.unit == null ? null : master?.UnitExpansion?.Find(current.unit.expansion_rank);
                var time = GameSystem.This()?.Time;
                if (row != null && selected?.object_type == 9 && master?.Unit?.Find(selected.object_id) != null && capacity != null && time != null)
                    arguments = arguments with { RetryFix = new(row.id, selected.object_id, capacity.unit_count,
                        string.IsNullOrWhiteSpace(row.end_at) || time.IsActiveDate(row.start_at, row.end_at)) };
            }
        }
        if (api == "get_clear_history_teaminfo")
        {
            var query = request.TryCast<ClearHistoryTeamInfoRequest>();
            var master = MasterDB.This();
            var units = master?.Unit?.ToList()?.ToArray();
            var costumes = master?.Costume?.ToList()?.ToArray();
            var pictures = master?.Picture?.ToList()?.ToArray();
            if (query != null)
                arguments = new LocalArguments(HistoryQuery: new(query.kind, query.uniq_id1, query.uniq_id2, query.type, query.player_id),
                    HistoryDefinitions: units == null || costumes == null || pictures == null ? null
                        : new(units.Select(row => row.id).ToArray(), costumes.ToDictionary(row => row.id, row => row.unit_id),
                            pictures.Select(row => row.id).ToArray()));
        }
        if (api == "first_draw_save" && request.TryCast<FirstGachaSaveRequest>() is not null)
        {
            var master = MasterDB.This();
            var current = User.This();
            var unitCapacity = current?.unit == null ? null : master?.UnitExpansion?.Find(current.unit.expansion_rank);
            var pictureCapacity = current?.picture == null ? null : master?.PictureExpansion?.Find(current.picture.expansion_rank);
            var ids = Plugin.Server.PendingIntroPictures;
            if (unitCapacity != null && pictureCapacity != null && ids.All(id => master?.Picture?.Find(id) != null))
                arguments = new LocalArguments(IntroGrant: new(unitCapacity.unit_count, pictureCapacity.picture_count,
                    ids.Select(id =>
                    {
                        var picture = UserData.UserPicture.CreateUserPicture(id);
                        return new LocalIntroPicture(id, picture.parameter1_value, picture.parameter2_value,
                            picture.picture_characteristic_group_master_id);
                    }).ToArray()));
        }
        if (api == "first_draw_data")
        {
            var change = request.TryCast<FirstGachaDrawRequest>();
            var master = MasterDB.This();
            var units = master?.Unit?.ToList()?.ToArray();
            var pictures = master?.Picture?.ToList()?.ToArray();
            arguments = new LocalArguments(Text: change?.public_id, IntroDefinitions: units == null || pictures == null ? null
                : new(units.Select(row => row.id).ToArray(), pictures.Select(row => row.id).ToArray()));
        }
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
                    3 => setting.GOLD_MAX, 6 => setting.CRYSTAL_MAX,
                    12 when master?.Item?.Find(row.object_id) != null => setting.ITEM_MAX, _ => 0
                };
                arguments = new LocalArguments(GoldExchange: new(change.shop_item_id, row.id, change.count, row.consume_gold,
                    string.IsNullOrWhiteSpace(row.end_at) || time.IsActiveDate(row.start_at, row.end_at),
                    new(row.object_type, row.object_id, row.object_value, capacity)));
            }
        }
        if (api == "update_gold_exchange_shop_list" && request.TryCast<UpdateGoldExchangeShopListRequest>() is not null)
        {
            var master = MasterDB.This();
            var costs = master?.GoldExchangeUpdate?.ToList()?.ToArray().OrderBy(row => row.id).ToArray();
            var goods = master?.GoldExchange?.ToList()?.ToArray();
            var time = GameSystem.This()?.Time;
            if (costs != null && goods != null && time != null
                && costs.Select((row, index) => row.id == index + 1).All(valid => valid))
                arguments = new LocalArguments(ShopRefresh: new(costs.Select(row => row.consume_orb).ToArray(),
                    goods.Where(row => string.IsNullOrWhiteSpace(row.end_at) || time.IsActiveDate(row.start_at, row.end_at)).Select(row => row.id).ToArray()));
        }
        if (api == "unit_reincarnation_release") arguments = ReadAutomaticRelease(request.TryCast<ReincarnationReleaseRequest>());
        if (api == "votive_picture_exchange") arguments = ReadTicketExchange(request.TryCast<VotivePictureExchangeRequest>());
        if (api == "fudai/fudai_info" && request.TryCast<TreasureInfoRequest>() is not null)
            arguments = new LocalArguments(Value: MasterDB.This()?.Event?.ToList()?.ToArray().FirstOrDefault(row => row.event_type == 99)?.id);
        if (api == "get_event_total_point_reward")
            arguments = ReadEventRewards(request.TryCast<GetEventTotalPointRewardRequest>()?.event_id);
        if (api == "get_event_total_point_reward_all")
            arguments = ReadEventRewards(request.TryCast<GetEventTotalPointRewardAllRequest>()?.event_id);
        if (api == "gain_free_item" && request.TryCast<GainFreeItemRequest>() is not null)
        {
            var master = MasterDB.This();
            var row = master?.FreeItem?.Find(Plugin.Server.FreeItemDefinitionId());
            var setting = master?.Setting;
            if (row != null && setting != null)
            {
                var capacity = row.object_type switch
                {
                    3 => setting.GOLD_MAX, 6 => setting.CRYSTAL_MAX,
                    12 when master?.Item?.Find(row.object_id) != null => setting.ITEM_MAX, _ => 0
                };
                arguments = new LocalArguments(FreeItem: new(row.id, new(row.object_type, row.object_id, row.object_value, capacity)));
            }
        }
        if (api == "pass_card/buy_goods")
        {
            var change = request.TryCast<BuyGoodsRequest>();
            var master = MasterDB.This();
            var row = change == null ? null : master?.PassCardShop?.Find(change.goods_id);
            var setting = master?.Setting;
            arguments = new LocalArguments(Value: change?.goods_id);
            if (row != null && setting != null)
            {
                var capacity = row.object_type switch
                {
                    3 => setting.GOLD_MAX, 6 => setting.CRYSTAL_MAX,
                    12 when master?.Item?.Find(row.item_id) != null => setting.ITEM_MAX, _ => 0
                };
                arguments = arguments with { PassGoods = new(row.id, row.consume_coin, row.limit_times,
                    new(row.object_type, row.item_id, row.value, capacity)) };
            }
        }
        if (api == "pass_card/user_orb_add_level")
            arguments = ReadPassRewardDefinitions(new LocalArguments(Value: request.TryCast<AddLevelRequest>()?.orb));
        if (api == "pass_card/get_level_gift" && request.TryCast<LevelGiftRequest>() is not null)
            arguments = ReadPassRewardDefinitions(new LocalArguments());
        if (api == "pass_card/show_reward" && request.TryCast<ShowRewardRequest>() is not null)
            arguments = ReadPassRewardDefinitions(new LocalArguments());
        if (api == "pass_card/collect_reward") arguments = ReadPassRewards(request.TryCast<CollectRewardRequest>());
        if (api == "pass_card/collect_exp")
        {
            var change = request.TryCast<CollectExpRequest>();
            var master = MasterDB.This();
            var row = change == null ? null : master?.PassCardTask?.Find(change.task_id);
            var levels = master?.PassCard?.ToList()?.ToArray();
            arguments = new LocalArguments(Value: change?.task_id);
            if (row != null && levels != null && levels.All(entry => entry.id == entry.level))
                arguments = arguments with { PassTask = new(row.id, row.exp,
                    levels.Where(entry => entry.GetFreeParam()?.free_data?.Count > 0).Select(entry => entry.level).ToArray(),
                    levels.Where(entry => entry.GetVipParam()?.pay_data?.Count > 0).Select(entry => entry.level).ToArray()) };
        }
        if (api == "get_exchange_code_reward")
        {
            var change = request.TryCast<ExchangeCodeRequest>();
            var master = MasterDB.This();
            var setting = master?.Setting;
            arguments = new LocalArguments(CodeExchange: change == null ? null : new(change.exchange_code, change.user_id, change.device_id),
                MailRules: setting == null ? null : new(setting.GOLD_MAX, setting.CRYSTAL_MAX, setting.ITEM_MAX,
                    master?.Item?.ToList()?.ToArray().Select(item => item.id).ToArray()));
        }
        if (api == "picture_book/get_reward")
        {
            var change = request.TryCast<PictureBookReciveRequest>();
            var master = MasterDB.This();
            var row = change == null ? null : master?.PictureBookTask?.Find(change.task_id);
            arguments = new LocalArguments(Value: change?.task_id);
            if (row != null && master?.Setting != null)
            {
                var capacity = row.reward_object_type switch
                {
                    3 => master.Setting.GOLD_MAX, 6 => master.Setting.CRYSTAL_MAX,
                    12 when master.Item?.Find(row.reward_object_id) != null => master.Setting.ITEM_MAX, _ => 0
                };
                arguments = arguments with { BookReward = new(row.id, row.picture_book_id,
                    new(row.reward_object_type, row.reward_object_id, row.reward_object_value, capacity)) };
            }
        }
        if (api == "dispatch_start") arguments = ReadDispatchStart(request.TryCast<DispatchStartRequest>());
        if (api == "personal_info_update")
        {
            var change = request.TryCast<EventPersonalInfoUpdateAPIRequest>();
            if (change != null) arguments = new LocalArguments(ContactDetails: new(change.user_name, change.zip_code, change.address, change.phone_number));
        }
        if (api == "anniversary_info_update")
        {
            var change = request.TryCast<EventAnniversaryInfoUpdateAPIRequest>();
            arguments = new LocalArguments(Value: change?.is_agree, Text: change?.mail_address);
        }
        if (api == "item_exchange")
        {
            var change = request.TryCast<ItemExchangeRequest>();
            var master = MasterDB.This();
            var row = change == null ? null : master?.ItemExchange?.ToList()?.ToArray().FirstOrDefault(entry => entry.id == change.item_exchange_id);
            var source = row == null ? null : master?.Item?.Find(row.consume_item_id);
            var target = row == null ? null : master?.Item?.Find(row.exchange_item_id);
            arguments = new LocalArguments(change?.item_exchange_id, change?.exchange_count);
            if (row != null && source != null && target != null && source.type == row.item_type && row.exchange_count == 1 && master?.Setting != null)
                arguments = arguments with { ItemExchange = new(row.id, row.consume_item_id, row.consume_count, row.exchange_item_id, master.Setting.ITEM_MAX) };
        }
        if (api == "auto_replay_start")
        {
            var change = request.TryCast<AutoReplayStartRequest>();
            if (change != null) arguments = new LocalArguments(Value: change.execute_num,
                ReplayMeasure: new(change.quest_type, change.target_id, change.difficulty));
        }
        if (api == "user_twitter_info_update")
            arguments = new LocalArguments(Text: request.TryCast<UserTwitterInfoUpdateRequest>()?.twitter_user_id);
        if (api == "user_twitter_info_delete")
            arguments = new LocalArguments(Text: request.TryCast<UserTwitterInfoDeleteRequest>()?.twitter_user_id);
        if (api == "lecture_room_unit_trust_up")
        {
            var change = request.TryCast<LectureRoomUnitTrustUpRequest>();
            arguments = new LocalArguments(change?.user_unit_id, MasterDB.This()?.UnitTrustGrowth?.Last()?.trust_upper_limit);
        }
        if (api == "picture_recycle")
            arguments = ReadPictureRecycle(request.TryCast<PictureRecycleRequest>());
        if (api == "picture_potential_limitbreak")
            arguments = ReadPictureLimitBreak(request.TryCast<PicturePotentialLimitbreakRequest>());
        if (api == "unit_rank_promote_item_set")
            arguments = ReadRankMaterial(request.TryCast<UnitRankPromoteItemSetRequest>());
        if (api == "unit_rank_promote")
            arguments = ReadRankPromotion(request.TryCast<UnitRankPromoteRequest>());
        if (api == "unit_spec_actualize")
            arguments = ReadStrengthening(request.TryCast<UnitSpecActualizeRequest>());
        if (api == "unit_extra_spec_actualize")
            arguments = ReadExtraStrengthening(request.TryCast<UnitExtraSpecActualizeRequest>());
        if (api == "limitbreak_strong_fragrantwood")
            arguments = ReadStatUnlock(request.TryCast<UnitLimitbreakStrongFragrantWoodRequest>());
        if (api == "picture_spec_actualize")
        {
            var change = request.TryCast<PictureSpecActualizeRequest>();
            var picture = change == null ? null : User.This()?.picture?.Find(change.user_picture_id);
            arguments = new LocalArguments(change?.user_picture_id);
            if (picture != null && picture.spec_actualize >= 0 && picture.spec_actualize < picture.spec_actualize_limit)
            {
                var status = new PictureStatus(picture.picture_id);
                var next = MasterDB.This()?.PictureSpecActualize?.Find(status.GetPictureType(), status.GetRare(), picture.spec_actualize + 1);
                if (next != null) arguments = arguments with { PictureGrowth = new(picture.picture_id, picture.spec_actualize,
                    next.spec_actualize, picture.spec_actualize_limit, next.item_id, next.item_value, next.consume_gold) };
            }
        }
        if (api == "picture_spec_actualize_chemicals")
        {
            var change = request.TryCast<PictureSpecActualizeChemicalRequest>();
            var picture = change == null ? null : User.This()?.picture?.Find(change.user_picture_id);
            arguments = new LocalArguments(change?.user_picture_id);
            if (picture != null && picture.spec_actualize >= 0 && picture.spec_actualize < picture.spec_actualize_limit)
            {
                var status = new PictureStatus(picture.picture_id);
                var master = MasterDB.This();
                var next = master?.PictureEnhanceChemical?.Find(status.GetPictureType(), status.GetRare(), picture.spec_actualize + 1);
                var growth = master?.PictureSpecActualize?.Find(status.GetPictureType(), status.GetRare(), picture.spec_actualize + 1);
                if (next != null && growth != null) arguments = arguments with { PictureGrowth = new(picture.picture_id, picture.spec_actualize,
                    next.spec_actualize, picture.spec_actualize_limit, next.item_id, next.item_value, 0) };
            }
        }
        if (api == "unit_destiny_value_up")
        {
            var change = request.TryCast<UnitDestinyValueUpRequest>();
            var unit = change == null ? null : User.This()?.unit?.Find(change.user_unit_id);
            var master = MasterDB.This();
            var definition = unit == null ? null : master?.Unit?.Find(unit.unit_id);
            var setting = master?.Setting;
            arguments = new LocalArguments(change?.user_unit_id, Ids: change?.material_user_unit_id_list?.ToArray());
            if (change != null && unit != null && definition != null && setting != null)
                arguments = arguments with { DestinyGrowth = new(unit.unit_id, unit.destiny_value, setting.DESTINY_VALUE_MAX,
                [
                    new(definition.limitbreak_item_id, change.material_item_num, setting.DESTINY_VALUE_ADD_FOR_PAPER_DOLL),
                    new(setting.DESTINY_VALUE_MATERIAL_ID, change.material_exclusive_item_num, 1),
                    new(definition.destiny_value_up_item_id, change.unit_exclusive_item_num, setting.DESTINY_VALUE_ADD_UNIT_EXCLUSIVE_ITEM)
                ]) };
        }
        if (api == "unit_level_limitbreak")
        {
            var change = request.TryCast<UnitLevelLimitbreakRequest>();
            var unit = change == null ? null : User.This()?.unit?.Find(change.user_unit_id);
            var master = MasterDB.This();
            var next = unit == null || unit.is_limitbreak_max || unit.level_limitbreak == int.MaxValue
                ? null : master?.UnitLevelLimitbreakGrowth?.Find(unit.level_limitbreak + 1);
            var definition = unit == null ? null : master?.Unit?.Find(unit.unit_id);
            var setting = master?.Setting;
            arguments = new LocalArguments(change?.user_unit_id, change?.material_item_num, change?.material_user_unit_id_list?.ToArray(),
                LimitBreak: unit == null || next == null || definition == null || setting == null ? null
                    : new(unit.unit_id, unit.level_limitbreak, next.level_limitbreak, next.level_lower_limit,
                        next.consume_gold, next.consume_same_unit_count, definition.limitbreak_item_id,
                        setting.DESTINY_VALUE_ADD_FOR_PAPER_DOLL, setting.DESTINY_VALUE_MAX));
        }
        if (api == "unit_rare_awaken")
        {
            var awakening = request.TryCast<UnitRareAwakenRequest>();
            var unit = awakening == null ? null : User.This()?.unit?.Find(awakening.user_unit_id);
            var next = unit == null || unit.is_rare_max || unit.rare == int.MaxValue
                ? null : MasterDB.This()?.UnitRareAwakenGrowth?.Find(unit.rare + 1);
            arguments = new LocalArguments(awakening?.user_unit_id,
                Awakening: unit == null || next == null ? null : new(unit.rare, next.rare, next.consume_piece));
        }
        if (api == "mail_receive")
        {
            var setting = MasterDB.This()?.Setting;
            var items = MasterDB.This()?.Item?.ToList()?.ToArray().Select(item => item.id).ToArray();
            arguments = new LocalArguments(Ids: request.TryCast<MailReceiveRequest>()?.receive_id_list?.ToArray(),
                MailRules: setting == null ? null : new(setting.GOLD_MAX, setting.CRYSTAL_MAX, setting.ITEM_MAX, items));
        }
        if (api == "friend_helper_select")
            arguments = new LocalArguments(Value: MasterDB.This()?.Setting?.FRIEND_COUNT_MAX);
        if (api == "phantasm_point_buy")
        {
            var nativeUser = User.This();
            var count = nativeUser?.phantasm_point?.purchase_count;
            var price = count is >= 0 and < int.MaxValue ? MasterDB.This()?.PhantasmPointShop?.Find(count.Value + 1) : null;
            var setting = MasterDB.This()?.Setting;
            arguments = new LocalArguments(PointPurchase: nativeUser == null || price == null || setting == null || !count.HasValue
                ? null : new(nativeUser.level, count.Value, price.consume_orb, setting.PHANTASM_POINT_BUY_BASE,
                    setting.PHANTASM_POINT_BUY_CALC_VALUE, setting.PHANTASM_POINT_MAX));
        }
        if (api == "gather_gold")
        {
            var level = User.This()?.level;
            arguments = new LocalArguments(EntityId: level.HasValue ? MasterDB.This()?.PlayerExp?.Find(level.Value)?.gold_max : null,
                Value: level);
        }
        if (api is "gold_box_renovation_start" or "gold_box_renovation_end")
        {
            var level = User.This()?.gold_box?.level;
            var next = level is > 0 and < int.MaxValue ? MasterDB.This()?.GoldBox?.Find(level.Value + 1) : null;
            arguments = new LocalArguments(EntityId: next?.level, Value: level,
                Renovation: next == null || !level.HasValue ? null : new(level.Value, next.level, next.need_player_level, next.consume_gold, next.need_time));
        }
        if (api == "gather_phantasm_point")
        {
            var level = User.This()?.level;
            arguments = new LocalArguments(EntityId: level.HasValue ? MasterDB.This()?.PlayerExp?.Find(level.Value)?.phantasm_point_max : null,
                Value: level);
        }
        if (api is "phantasm_box_renovation_start" or "phantasm_box_renovation_end")
        {
            var level = User.This()?.phantasm_box?.level;
            var next = level is > 0 and < int.MaxValue ? MasterDB.This()?.PhantasmBox?.Find(level.Value + 1) : null;
            arguments = new LocalArguments(EntityId: next?.level, Value: level,
                Renovation: next == null || !level.HasValue ? null : new(level.Value, next.level, next.need_player_level, next.consume_gold, next.need_time));
        }
        if (api == "get_quest_cleared_team_info")
        {
            var query = request.TryCast<GetQuestClearedTeamInfoRequest>();
            arguments = query == null ? null : new LocalArguments(ReplayMeasure: new(query.quest_type, query.target_id, query.difficulty));
        }
        if (api == "afk_set")
            arguments = new LocalArguments(Idle: ReadIdleSet(request.TryCast<IdleSearchSetRequest>()));
        if (api == "afk_complete_upgrade")
        {
            var completion = request.TryCast<IdleSearchCompleteUpgradeRequest>();
            arguments = completion == null ? null : new LocalArguments(EntityId: completion.slot_number, Value: completion.object_type,
                Idle: ReadIdleDefinition(Plugin.Server.IdleSelection(completion.object_type, completion.slot_number)));
        }
        if (api == "allocate_points")
        {
            var allocation = request.TryCast<UnitAllocatePointsRequest>();
            var rules = MasterDB.This()?.PointAllocation?.ToList()?.ToArray()
                .Select(rule => new LocalPointRule(rule.type, rule.step, rule.need_point, rule.required_total_used_point)).ToArray();
            arguments = allocation == null ? null : new LocalArguments(PointAllocation: new(allocation.user_unit_id,
                allocation.allocate_dict_list?.ToArray().Select(entry => new LocalPointTarget(entry.type, entry.step)).ToArray(), rules));
        }
        if (api == "reset_point_allocation")
        {
            var reset = request.TryCast<UnitResetPointAllocationRequest>();
            var setting = MasterDB.This()?.Setting;
            arguments = reset == null || setting == null ? null : new LocalArguments(PointReset:
                new(reset.user_unit_id, setting.PA_RESET_CONSUME_OBJECT_VALUE, setting.PA_RESET_FREE_FLAG >= 1));
        }
        if (api == "weekly_arena_battle_score_reset")
            arguments = new LocalArguments(Value: request.TryCast<WeeklyArenaBattleScoreResetRequest>()?.battle_number);
        if (api == "episode_end")
        {
            var episode = request.TryCast<EpisodeEndRequest>();
            var table = MasterDB.This()?.Episode;
            arguments = episode == null ? null : new LocalArguments(EntityId: episode.episode_id,
                Value: table == null ? null : table.Find(episode.episode_id)?.type ?? 0);
        }
        if (api == "friend/apply")
            arguments = new LocalArguments(Value: MasterDB.This()?.Setting?.FRIEND_COUNT_MAX,
                Text: request.TryCast<FriendApplyRequest>()?.public_id);
        if (api == "friend_request")
            arguments = new LocalArguments(EntityId: MasterDB.This()?.Setting?.FRIEND_COUNT_MAX,
                Value: MasterDB.This()?.Setting?.REQUEST_COUNT_MAX, Text: request.TryCast<FriendRequestRequest>()?.request_player_id);
        if (api == "friend_approve")
            arguments = new LocalArguments(Value: MasterDB.This()?.Setting?.FRIEND_COUNT_MAX,
                Text: request.TryCast<FriendApproveRequest>()?.approve_player_id);
        if (api == "friend_request_cancel")
        {
            var cancellation = request.TryCast<FriendRequestCancelRequest>();
            arguments = cancellation == null ? null : new LocalArguments(Value: cancellation.is_delete_all, Text: cancellation.cancel_player_id);
        }
        if (api == "friend_delete")
            arguments = new LocalArguments(Text: request.TryCast<FriendDeleteRequest>()?.delete_player_id);
        if (api == "friend/info")
            arguments = new LocalArguments(Text: request.TryCast<FriendInfoRequest>()?.public_id);
        if (api == "friend/give")
        {
            var gift = request.TryCast<FriendGiveRequest>();
            arguments = gift == null ? null : new LocalArguments(Value: gift.is_all ? 1 : 0, Text: gift.public_id);
        }
        if (api == "friend/receive")
        {
            var gift = request.TryCast<FriendGetRequest>();
            arguments = gift == null ? null : new LocalArguments(MasterDB.This()?.Setting?.GOLD_MAX,
                gift.is_all ? 1 : 0, Text: gift.public_id);
        }
        if (api == "friend/accept_apply")
            arguments = new LocalArguments(Value: MasterDB.This()?.Setting?.FRIEND_COUNT_MAX,
                Text: request.TryCast<FriendAcceptRequest>()?.public_id);
        if (api == "friend/refuse_apply")
        {
            var refusal = request.TryCast<FriendRefuseRequest>();
            arguments = refusal == null ? null : new LocalArguments(Value: refusal.is_all ? 1 : 0, Text: refusal.public_id);
        }
        if (api == "avatar/preview")
            arguments = new LocalArguments(Value: request.TryCast<previewAvatarAPIAsyncRequest>()?.avatar_id);
        if (api == "avatar/update")
            arguments = new LocalArguments(Value: request.TryCast<replaceAvatarRequest>()?.avatar_id);
        if (api == "training_room_shortened_finish_time")
        {
            var shortened = request.TryCast<TrainingRoomShortenedFinishTimeRequest>();
            var textbooks = MasterDB.This()?.Textbook?.ToList()?.ToArray();
            arguments = new LocalArguments(EntityId: shortened?.user_unit_id,
                TrainingMaterials: shortened?.material_item_list?.ToArray().Select(entry =>
                    new LocalTrainingMaterial(entry.item_id, entry.count,
                        textbooks?.FirstOrDefault(book => book.item_id == entry.item_id)?.shorten_time ?? 0)).ToArray());
        }
        if (api == "training_room_start")
        {
            var training = request.TryCast<TrainingRoomStartRequest>();
            var material = MasterDB.This()?.Textbook?.ToList()?.ToArray().FirstOrDefault(entry => entry.item_id == training?.material_item_id);
            var capacity = MasterDB.This()?.TrainingRoomExpansion?.Find(Plugin.Server.TrainingExpansionRank)?.slot_count;
            arguments = training == null || material == null || capacity == null ? null
                : new LocalArguments(Training: new(training.user_unit_id, training.skill_number, training.material_item_id,
                    training.slot_number, capacity.Value, material.need_time));
        }
        if (api == "lecture_room_food_distribution")
        {
            var distribution = request.TryCast<LectureRoomFoodDistributionRequest>();
            var food = MasterDB.This()?.Food?.ToList()?.ToArray().FirstOrDefault(entry => entry.item_id == distribution?.item_id);
            var capacity = MasterDB.This()?.LectureRoomFoodExpansion?.Find(Plugin.Server.LectureFoodExpansionRank)?.food_limit;
            arguments = distribution == null || food == null || capacity == null ? null
                : new LocalArguments(FoodDistribution: new(distribution.item_id, food.id, distribution.num, food.value, capacity.Value, food.add_effect_time));
        }
        if (api == "lecture_room_food_addition")
        {
            var purchase = request.TryCast<LectureRoomFoodAdditionRequest>();
            var food = MasterDB.This()?.Food?.ToList()?.ToArray().FirstOrDefault(entry => entry.item_id == purchase?.item_id);
            arguments = purchase == null || food == null ? null
                : new LocalArguments(FoodPurchase: new(purchase.item_id, purchase.num, food.consume_phantasm_point, food.consume_orb));
        }
        if (api == "series_battle_team_update")
            arguments = new LocalArguments(SeriesTeams: request.TryCast<SeriesBattleTeamUpdateRequest>()?.team_update_list?.ToArray()
                .Select(team => new LocalSeriesTeam(team.team_no, team.user_unit_id_list?.ToArray())).ToArray());
        if (api == "comic_get_pages")
        {
            var pages = request.TryCast<ComicGetPagesRequest>();
            var definition = pages == null ? null : MasterDB.This()?.Comic?.Find(pages.comic_id);
            var price = MasterDB.This()?.Setting?.GET_COMIC_PAGE_USED_POINT;
            arguments = pages == null || definition == null || price == null ? null
                : new LocalArguments(Pages: new(pages.comic_id, pages.page_count, definition.max_page_num, price.Value));
        }
        if (api == "comic_read_end")
        {
            var pages = request.TryCast<ComicReadEndRequest>();
            arguments = pages == null ? null : new LocalArguments(pages.comic_id, pages.page_number);
        }
        if (api == "roguelite_top")
            arguments = new LocalArguments(Value: MasterDB.This()?.Setting?.MAX_DECK_NO);
        if (api == "roguelite_deck_call")
            arguments = new LocalArguments(Value: request.TryCast<RogueliteDeckCallRequest>()?.deck_no);
        if (api == "roguelite_deck_save")
        {
            var deck = request.TryCast<RogueliteDeckSaveRequest>();
            arguments = deck == null ? null : new LocalArguments(Value: deck.deck_no,
                Ids: deck.deck_list?.ToArray().Select(id => (long)id).ToArray(), Text: deck.deck_name);
        }
        if (api == "achievement_select")
            arguments = new LocalArguments(Value: request.TryCast<AchievementSelectRequest>()?.achievement_id);
        if (api == "rhythm_game_start")
        {
            var id = request.TryCast<RhythmGameStartRequest>()?.rhythm_game_music_master_id;
            arguments = new LocalArguments(id, id is > 0 && MasterDB.This()?.RhythmGameMusic?.Find(id.Value) != null ? 1 : 0);
        }
        if (api == "rhythm_game_end")
        {
            var result = request.TryCast<RhythmGameEndRequest>();
            arguments = result == null ? null : new LocalArguments(Rhythm: new(result.rhythm_game_music_master_id,
                result.difficulty, result.score, result.rate, result.rank));
        }
        if (api == "logout")
            arguments = new LocalArguments(Text: request.TryCast<LogoutRequest>()?.user_id);
        if (api == "set_age")
            arguments = new LocalArguments(Value: request.TryCast<SetAgeRequest>()?.age_type);
        if (api == "read_unit_talk")
            arguments = new LocalArguments(Value: request.TryCast<ReadUnitTalkRequest>()?.talk_id);
        if (api == "training_room_cancel")
            arguments = new LocalArguments(Value: request.TryCast<TrainingRoomCancelRequest>()?.slot_number);
        if (api == "dispatch_cancel")
            arguments = new LocalArguments(EntityId: request.TryCast<DispatchCancelRequest>()?.user_dispatch_id);
        if (api == "unit_equip_picture_reset")
            arguments = new LocalArguments(EntityId: request.TryCast<UnitEquipPictureResetRequest>()?.user_unit_id);
        if (api == "unit_equip_costume")
        {
            var update = request.TryCast<UnitEquipCostumeRequest>();
            arguments = new LocalArguments(Costumes: update?.costume_list?.ToArray().Select(entry =>
            {
                if (entry == null) return new LocalCostumeEntry(0, 0, 0, false);
                var definition = MasterDB.This()?.Costume?.Find(entry.costume_id);
                var unit = definition == null ? null : MasterDB.This()?.Unit?.Find(definition.unit_id);
                return new LocalCostumeEntry(entry.user_unit_id, entry.costume_id, definition?.unit_id ?? 0,
                    unit?.default_costume_id == entry.costume_id);
            }).ToArray());
        }
        if (api == "unit_equip_picture")
        {
            var update = request.TryCast<UnitEquipPictureRequest>();
            arguments = update == null ? null : new LocalArguments(update.user_unit_id,
                Equipment: update.picture_list?.ToArray().Select(entry => entry == null
                    ? new LocalEquipmentEntry(0, 0) : new LocalEquipmentEntry(entry.spellcard_number, entry.user_picture_id)).ToArray());
        }
        if (api == "lecture_room_unit_remove")
            arguments = new LocalArguments(EntityId: request.TryCast<LectureRoomUnitRemoveRequest>()?.user_unit_id);
        if (api == "lecture_room_unit_set")
        {
            var capacity = MasterDB.This()?.LectureRoomUnitExpansion?.Find(Plugin.Server.LectureExpansionRank)?.slot_count;
            arguments = new LocalArguments(request.TryCast<LectureRoomUnitSetRequest>()?.user_unit_id, capacity);
        }
        if (api == "lecture_room_unit_expansion")
        {
            var rank = Plugin.Server.LectureExpansionRank;
            var next = rank == int.MaxValue ? null : MasterDB.This()?.LectureRoomUnitExpansion?.Find(rank + 1);
            arguments = next == null ? null : new LocalArguments(rank + 1, next.consume_orb);
        }
        if (api == "lecture_room_food_expansion")
        {
            var rank = Plugin.Server.LectureFoodExpansionRank;
            var next = rank == int.MaxValue ? null : MasterDB.This()?.LectureRoomFoodExpansion?.Find(rank + 1);
            arguments = next == null ? null : new LocalArguments(rank + 1, next.consume_orb);
        }
        if (api == "training_room_expansion")
        {
            var rank = Plugin.Server.TrainingExpansionRank;
            var next = rank == int.MaxValue ? null : MasterDB.This()?.TrainingRoomExpansion?.Find(rank + 1);
            arguments = next == null ? null : new LocalArguments(rank + 1, next.consume_orb);
        }
        if (api == "transcribe_kpi")
            arguments = new LocalArguments(Value: request.TryCast<TranscribeKpiRequest>()?.transcribe_value);
        if (api == "unit_voice_update")
        {
            var update = request.TryCast<UnitVoiceUpdateRequest>();
            arguments = update == null ? null : new LocalArguments(update.unit_id, update.voice_set_id);
        }
        if (api == "home_select_unit_update")
            arguments = new LocalArguments(Value: request.TryCast<HomeCustomSelectUnitUpdateRequest>()?.select_unit_index);
        if (api == "favorite_unit_update")
        {
            var update = request.TryCast<HomeCustomSetUpdateRequest>();
            arguments = update == null ? null : new LocalArguments(Home: new(update.unit_param?.ToArray()
                .Select(entry => entry == null ? new LocalHomeEntry(0, 0, 0, 0, 0)
                    : new LocalHomeEntry(entry.unit_id, entry.user_unit_id, entry.costume_id, entry.home_motion, entry.bgm)).ToArray(),
                update.select_unit_index, update.random_select_mode));
        }
        if (api == "home_bgm_update")
            arguments = new LocalArguments(Value: request.TryCast<HomeBGMUpdateRequest>()?.select_bgm_id);
        if (api == "update_home_motion_mode")
        {
            var update = request.TryCast<UpdateHomeMotionModeRequest>();
            arguments = update == null ? null : new LocalArguments(update.user_unit_id, update.mode);
        }
        if (api == "unit_bgm_update")
        {
            var update = request.TryCast<UnitBGMUpdateRequest>();
            arguments = update == null ? null : new LocalArguments(update.user_unit_id, update.select_bgm_id);
        }
        if (api == "friend_setting_helper")
        {
            var update = request.TryCast<FriendSettingHelperRequest>();
            arguments = update == null ? null : new LocalArguments(update.user_unit_id1, update.total_power);
        }
        if (api == "team_update")
        {
            var update = request.TryCast<TeamUpdateRequest>();
            arguments = update == null ? null : new LocalArguments(Team: new(update.number, update.name,
                update.user_unit_id_list?.ToArray(), update.use_cost, update.season_enhancement_user_unit_id_list?.ToArray(),
                update.use_cost_with_friend));
        }
        if (api == "achievement_new_update")
            arguments = new LocalArguments(Ids: request.TryCast<AchievementNewUpdateRequest>()?.new_update_id_list?.ToArray()
                .Select(id => (long)id).ToArray());
        if (api is "get_replay_info" or "update_replay_info")
        {
            var replay = request.TryCast<BattleReplayInfoRequest>();
            arguments = replay == null ? null : new LocalArguments(Text: request.TryCast<UpdateReplayInfoRequest>()?.replay_info,
                ReplayMeasure: new(replay.quest_type, replay.target_id, replay.difficulty));
        }
        if (api == "get_series_battle_replay_info")
        {
            var query = request.TryCast<SeriesBattleGetReplayInfoRequest>();
            arguments = query == null ? null : new LocalArguments(EntityId: query.stage_id,
                Ids: query.area_index?.ToArray().Select(area => (long)area).ToArray());
        }
        if (api == "update_series_battle_replay_info")
        {
            var update = request.TryCast<SeriesBattleUpdateReplayInfoRequest>();
            arguments = update == null ? null : new LocalArguments(update.stage_id, update.area_index, Text: update.replay_info);
        }
        if (api == "get_auto_replay_info")
        {
            var query = request.TryCast<GetAutoReplayInfoRequest>();
            arguments = query == null ? null : new LocalArguments(ReplayMeasure: new(query.quest_type, query.target_id, query.difficulty));
        }
        if (api == "get_multiple_spirits_weekly_replay_info")
            arguments = new LocalArguments(EntityId: request.TryCast<GetVersusReplayInfoRequest>()?.multiple_spirits_weekly_master_id);
        if (api == "update_multiple_spirits_weekly_replay_info")
        {
            var update = request.TryCast<UpdateVersusReplayInfoRequest>();
            arguments = update == null ? null : new LocalArguments(ReplayContent: new(update.multiple_spirits_weekly_master_id,
                update.level, update.add_rules, update.replay_info));
        }
        if (api == "update_auto_replay_info")
        {
            var update = request.TryCast<UpdateAutoReplayInfoRequest>();
            arguments = update == null ? null : new LocalArguments(ReplayMeasure: new(update.quest_type, update.target_id,
                update.difficulty, update.win_rate, update.action_num));
        }
        if (api == "update_profile_setting")
            arguments = new LocalArguments(Text: request.TryCast<UpdateProfileSettingAPIRequest>()?.profile_setting);
        if (api == "update_profile_setting2")
            arguments = new LocalArguments(Text: request.TryCast<UpdateProfileSetting2APIRequest>()?.text_string);
        if (api == "user_info_update")
        {
            var names = request.TryCast<Comic.UserInfoUpdateRequest>();
            arguments = new LocalArguments(Profile: new LocalProfileUpdate(
                names?.player_name ?? request.TryCast<UserPlayerNameUpdateRequest>()?.player_name,
                names?.hero_name ?? request.TryCast<UserHeroNameUpdateRequest>()?.hero_name,
                request.TryCast<UserCommentUpdateRequest>()?.comment));
        }
        if (api == "tutorial_update")
            arguments = new LocalArguments(Value: request.TryCast<TutorialUpdateRequest>()?.progress_value);
        if (api == "unit_voice_autoset_update")
            arguments = new LocalArguments(Value: request.TryCast<UnitVoiceAutosetUpdateRequest>()?.autoset_flag);
        if (api == "picture_protection")
        {
            var update = request.TryCast<PictureProtectionRequest>();
            arguments = update == null ? null : new LocalArguments(update.user_picture_id, update.is_protection);
        }
        if (api == "unit_new_update")
        {
            var update = request.TryCast<UnitNewUpdateRequest>();
            arguments = new LocalArguments(Ids: update?.user_unit_id_list?.ToArray());
        }
        if (api == "picture_new_update")
        {
            var update = request.TryCast<PictureNewUpdateRequest>();
            arguments = new LocalArguments(Ids: update?.user_picture_id_list?.ToArray());
        }
        var reply = Plugin.Server.Dispatch(api, gacha?.count, battle?.battle_end?.result, gacha?.gacha_id, gacha?.gacha_button_id, arguments);
        Log.Info($"Offline API: {api}; status={reply.Status}");
        network.m_recv.status = reply.Status;
        network.m_recv.now = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        network.m_recv.error_message = reply.Error;
        network.m_recv.body = "";
        if (reply.Status != 0)
        {
            if (api is "gacha_point_exchange" or "mission_end" or "battle_pass_mission_end" or "battle_pass_receive_reward" or "get_series_battle_ranking_teaminfo" or "mini_game_top")
            {
                var panel = UnityEngine.Object.FindObjectOfType<MissionBehavior>();
                if ((api is "mission_end" or "battle_pass_mission_end" or "battle_pass_receive_reward") && panel != null) panel.m_rcvflg = false;
                SceneSystem.This()?.IndicatorActive(false);
                PentopSystem.This()?.EnableEvent();
                PopupSystem.This()?.CreateMessage(reply.Error);
                yield break;
            }
            if (api == "friend_get_history")
            {
                var panel = UnityEngine.Object.FindObjectOfType<FriendPlayerDetailPanel>();
                if (panel != null)
                {
                    panel.m_friendBehavior?.ShowLRPanel();
                    panel.m_detailBG?.SetActive(false);
                    panel.gameObject.SetActive(false);
                }
                PentopSystem.This()?.EnableEvent();
                PopupSystem.This()?.CreateMessage(reply.Error);
                yield break;
            }
            if (api is "get_clear_history_playerinfo" or "retry_gacha_fix" or "dispatch_end" or "unit_recycle" or "get_clear_history_teaminfo" or "first_draw_data" or "exchange" or "update_gold_exchange_shop_list" or "votive_picture_exchange" or "gain_free_item" or "get_event_total_point_reward" or "get_event_total_point_reward_all")
            {
                if (api == "dispatch_end" && ErrandBehaviour.This() is { } errand)
                {
                    errand.m_isPlayingEndApi = false;
                    errand.m_doUpdate = true;
                }
                PentopSystem.This()?.EnableEvent();
                PopupSystem.This()?.CreateMessage(reply.Error);
                yield break;
            }
            if (api == "user_info_update")
            {
                var profileResponse = response.TryCast<Comic.UserInfoUpdateResponse>();
                if (profileResponse != null) profileResponse.edit_validity = 1;
            }
            // 本地缺失接口明确结束，不发起真实网络请求或无限重试。
            action?.Invoke(reply.Status, reply.Error);
            yield break;
        }
        // IDA: OnSuccessAPI 会解密/解压 m_recv.body，再更新会话、时间、用户数据及回调。
        network.m_recv.body = ResponseProtocol.Encode(api, reply.Body!.ToJsonString(), network.m_enckey);
        network.OnSuccessAPI(request, response, action);
        Log.Info($"Offline callback completed: {api}; loggedIn={AppLogin.IsLoggedIn()}");
    }

    [HarmonyPatch(nameof(NetworkSystem.TransceiveRoutine), typeof(string), typeof(TextCallback))]
    [HarmonyPrefix]
    public static bool TransceiveRoutine_Get_Prefix(string url, TextCallback action, ref NativeRoutine __result)
    {
        __result = RespondRaw(url, action).WrapToIl2Cpp();
        return SkipOriginal;
    }

    [HarmonyPatch(nameof(NetworkSystem.TransceiveRoutine), typeof(string), typeof(string), typeof(TextCallback))]
    [HarmonyPrefix]
    public static bool TransceiveRoutine_Post_Prefix(string url, TextCallback action, ref NativeRoutine __result)
    {
        __result = RespondRaw(url, action).WrapToIl2Cpp();
        return SkipOriginal;
    }

    private static IEnumerator RespondRaw(string url, TextCallback action)
    {
        yield return null;
        if (url == GGPublishLoaderPatch.LocalUrl)
        {
            Log.Info("Offline version manifest returned.");
            action?.Invoke(OfflineServer.VersionManifest(AssetLoaderSettings.AppVersion));
            yield break;
        }
        var endpoint = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.GetLeftPart(UriPartial.Path) : "relative-url";
        Log.Warning($"Unsupported raw game request blocked: {endpoint}; no HTTP.");
        action?.Invoke("{\"status\":501,\"body\":\"\",\"error_message\":\"Offline raw request not implemented\"}");
    }

    [HarmonyPatch(nameof(NetworkSystem.TransmitForget), typeof(RequestPacket))]
    [HarmonyPrefix]
    public static bool TransmitForget_Prefix(RequestPacket __0)
    {
        if (__0.GetAPIName() == "logout")
        {
            var reply = Plugin.Server.Dispatch("logout", arguments: new(Text: __0.TryCast<LogoutRequest>()?.user_id));
            Log.Info($"Offline logout: status={reply.Status}");
        }
        return SkipOriginal;
    }
}
