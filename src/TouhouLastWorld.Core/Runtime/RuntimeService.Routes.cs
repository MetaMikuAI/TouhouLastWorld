using System.Text.Json.Nodes;
using System.Text.Json;
namespace TouhouLastWorld;

internal sealed partial class RuntimeService
{
    private Dictionary<string, Func<IApiInput?, LocalReply?>> BuildRoutes()
    {
        var routes = new Dictionary<string, Func<IApiInput?, LocalReply?>>(StringComparer.Ordinal);
        routes["game_init"] = _ => null;
        routes["version_get"] = _ => null;
        routes["user_create"] = _ => null;
        routes["login"] = _ => null;
        routes["check_user"] = _ => null;
        routes["battle_start"] = _ => null;
        routes["battle_end"] = _ => null;
        routes["gacha_do"] = _ => null;
        routes["gacha_top"] = _ => null;
        routes["home"] = _ => null;
        routes["check_conditional_pack"] = _ => null;
        routes["afk_top"] = _ => null;
        routes["unit_protection"] = _ => null;
        routes["unit_new_update"] = _ => null;
        routes["picture_protection"] = _ => null;
        routes["picture_new_update"] = _ => null;
        routes["mail_top"] = _ => null;
        routes["friend/list"] = _ => null;
        routes["unit_voice_autoset_update"] = _ => null;
        routes["friend/apply_list"] = _ => null;
        routes["friend/recommend"] = _ => null;
        routes["get_shop_list"] = _ => null;
        foreach (var entry in QueryCatalog.QueryTemplates)
            routes[entry.Key] = _ => new(0, entry.Value.DeepClone().AsObject());
        routes["friend/give"] = payload =>
        {
            if (payload is not null && payload is not ApiFriendGiveInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiFriendGiveInput;
            return SocialService.GiveContactGift(arguments is { } requestData0 ? new GiveContactGiftCommand(Text: requestData0.Text, Value: requestData0.Value) : null);
        };
        routes["item_exchange"] = payload =>
        {
            if (payload is not null && payload is not ApiItemExchangeInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiItemExchangeInput;
            return ShopService.ExchangeItems(arguments is { } requestData1 ? new ExchangeItemsCommand(EntityId: requestData1.EntityId, ItemExchange: requestData1.ItemExchange, Value: requestData1.Value) : null);
        };
        routes["pass_card/buy_goods"] = payload =>
        {
            if (payload is not null && payload is not ApiPassCardBuyGoodsInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiPassCardBuyGoodsInput;
            return ShopService.BuyPassGoods(arguments is { } requestData2 ? new BuyPassGoodsCommand(PassGoods: requestData2.PassGoods, Value: requestData2.Value) : null);
        };
        routes["pass_card/user_orb_add_level"] = payload =>
        {
            if (payload is not null && payload is not ApiPassCardUserOrbAddLevelInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiPassCardUserOrbAddLevelInput;
            return ShopService.BuyPassLevels(arguments is { } requestData3 ? new BuyPassLevelsCommand(PassRewards: requestData3.PassRewards, Value: requestData3.Value) : null);
        };
        routes["pass_card/get_level_gift"] = payload =>
        {
            if (payload is not null && payload is not ApiPassCardGetLevelGiftInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiPassCardGetLevelGiftInput;
            return ShopService.BuyPassLevels(arguments is { } requestData4 ? new BuyPassLevelsCommand(PassRewards: requestData4.PassRewards, Value: requestData4.Value) : null, gift: true);
        };
        routes["pass_card/show_reward"] = payload =>
        {
            if (payload is not null && payload is not ApiPassCardShowRewardInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiPassCardShowRewardInput;
            return ShopService.ShowPassRewards(arguments is { } requestData5 ? new ShowPassRewardsCommand(PassRewards: requestData5.PassRewards) : null);
        };
        routes["gain_free_item"] = payload =>
        {
            if (payload is not null && payload is not ApiGainFreeItemInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiGainFreeItemInput;
            return ShopService.GainFreeItem(arguments is { } requestData6 ? new GainFreeItemCommand(FreeItem: requestData6.FreeItem) : null);
        };
        routes["get_event_total_point_reward"] = payload =>
        {
            if (payload is not null && payload is not ApiGetEventTotalPointRewardInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiGetEventTotalPointRewardInput;
            return EventsService.ClaimEventPointReward(arguments is { } requestData7 ? new ClaimEventPointRewardCommand(EventRewards: requestData7.EventRewards, Value: requestData7.Value) : null);
        };
        routes["get_event_total_point_reward_all"] = payload =>
        {
            if (payload is not null && payload is not ApiGetEventTotalPointRewardAllInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiGetEventTotalPointRewardAllInput;
            return EventsService.ClaimEventPointReward(arguments is { } requestData8 ? new ClaimEventPointRewardCommand(EventRewards: requestData8.EventRewards, Value: requestData8.Value) : null, all: true);
        };
        routes["multiple_spirits_weekly_top"] = payload =>
        {
            if (payload is not null && payload is not ApiMultipleSpiritsWeeklyTopInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiMultipleSpiritsWeeklyTopInput;
            return BattleModesService.VersusOverview(arguments?.Ids);
        };
        routes["roguelite_reroll"] = payload =>
        {
            if (payload is not null && payload is not ApiRogueliteRerollInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiRogueliteRerollInput;
            return BattleModesService.Reroll(arguments?.Reroll);
        };
        routes["batch_search"] = payload =>
        {
            if (payload is not null && payload is not ApiBatchSearchInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiBatchSearchInput;
            return BattleModesService.BatchSearch(arguments?.Batch);
        };
        routes["multiple_spirits_weekly_battle_end"] = payload =>
        {
            if (payload is not null && payload is not ApiMultipleSpiritsWeeklyBattleEndInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiMultipleSpiritsWeeklyBattleEndInput;
            return BattleModesService.EndVersus(arguments?.VersusEnd);
        };
        routes["multiple_spirits_weekly_battle_start"] = payload =>
        {
            if (payload is not null && payload is not ApiMultipleSpiritsWeeklyBattleStartInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiMultipleSpiritsWeeklyBattleStartInput;
            return BattleModesService.StartVersus(arguments?.VersusStart);
        };
        routes["arena_battle_end"] = payload =>
        {
            if (payload is not null && payload is not ApiArenaBattleEndInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiArenaBattleEndInput;
            return BattleModesService.EndArena(arguments?.ArenaEnd);
        };
        routes["arena_battle_start"] = payload =>
        {
            if (payload is not null && payload is not ApiArenaBattleStartInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiArenaBattleStartInput;
            return BattleModesService.StartArena(arguments?.ArenaStart);
        };
        routes["event_point_gacha_do"] = payload =>
        {
            if (payload is not null && payload is not ApiEventPointGachaDoInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiEventPointGachaDoInput;
            return EventsService.DrawEventPoints(arguments is { } requestData9 ? new DrawEventPointsCommand(EventDraw: requestData9.EventDraw) : null);
        };
        routes["fudai/fudai_draw"] = payload =>
        {
            if (payload is not null && payload is not ApiFudaiFudaiDrawInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiFudaiFudaiDrawInput;
            return EventsService.DrawTreasure(arguments is { } requestData10 ? new DrawTreasureCommand(TreasureDraw: requestData10.TreasureDraw, Value: requestData10.Value) : null);
        };
        routes["fudai/fudai_info"] = payload =>
        {
            if (payload is not null && payload is not ApiFudaiFudaiInfoInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiFudaiFudaiInfoInput;
            return EventsService.TreasureOverview(arguments is { } requestData11 ? new TreasureOverviewCommand(Value: requestData11.Value) : null);
        };
        routes["votive_picture_exchange"] = payload =>
        {
            if (payload is not null && payload is not ApiVotivePictureExchangeInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiVotivePictureExchangeInput;
            return ShopService.ExchangeTicket(arguments is { } requestData12 ? new ExchangeTicketCommand(TicketExchange: requestData12.TicketExchange) : null);
        };
        routes["unit_reincarnation_release"] = payload =>
        {
            if (payload is not null && payload is not ApiUnitReincarnationReleaseInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiUnitReincarnationReleaseInput;
            return UnitsService.ReleaseAutomaticNode(arguments is { } requestData13 ? new ReleaseAutomaticNodeCommand(AutomaticRelease: requestData13.AutomaticRelease, EntityId: requestData13.EntityId, Value: requestData13.Value) : null);
        };
        routes["update_gold_exchange_shop_list"] = payload =>
        {
            if (payload is not null && payload is not ApiUpdateGoldExchangeShopListInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiUpdateGoldExchangeShopListInput;
            return ShopService.RefreshExchangeShop(arguments is { } requestData14 ? new RefreshExchangeShopCommand(ShopRefresh: requestData14.ShopRefresh) : null);
        };
        routes["exchange"] = payload =>
        {
            if (payload is not null && payload is not ApiExchangeInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiExchangeInput;
            return ShopService.ExchangeGoldGoods(arguments is { } requestData15 ? new ExchangeGoldGoodsCommand(GoldExchange: requestData15.GoldExchange) : null);
        };
        routes["first_draw_data"] = payload =>
        {
            if (payload is not null && payload is not ApiFirstDrawDataInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiFirstDrawDataInput;
            return GachaService.PrepareFirstDraw(arguments is { } requestData16 ? new PrepareFirstDrawCommand(IntroDefinitions: requestData16.IntroDefinitions, Text: requestData16.Text) : null);
        };
        routes["first_draw_save"] = payload =>
        {
            if (payload is not null && payload is not ApiFirstDrawSaveInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiFirstDrawSaveInput;
            return GachaService.ConfirmFirstDraw(arguments is { } requestData17 ? new ConfirmFirstDrawCommand(IntroGrant: requestData17.IntroGrant) : null);
        };
        routes["retry_gacha_fix"] = payload =>
        {
            if (payload is not null && payload is not ApiRetryGachaFixInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiRetryGachaFixInput;
            return GachaService.FixRetryDraw(arguments is { } requestData18 ? new FixRetryDrawCommand(RetryFix: requestData18.RetryFix, Value: requestData18.Value) : null);
        };
        routes["gacha_point_exchange"] = payload =>
        {
            if (payload is not null && payload is not ApiGachaPointExchangeInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiGachaPointExchangeInput;
            return GachaService.ExchangeDrawPoints(arguments?.PointExchange);
        };
        routes["tutorial_first_gacha_do"] = payload =>
        {
            if (payload is not null && payload is not ApiTutorialFirstGachaDoInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiTutorialFirstGachaDoInput;
            return GachaService.TutorialInventory();
        };
        routes["tutorial_first_gacha_confirm"] = payload =>
        {
            if (payload is not null && payload is not ApiTutorialFirstGachaConfirmInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiTutorialFirstGachaConfirmInput;
            return GachaService.TutorialInventory(restore: true);
        };
        routes["get_clear_history_teaminfo"] = payload =>
        {
            if (payload is not null && payload is not ApiGetClearHistoryTeaminfoInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiGetClearHistoryTeaminfoInput;
            return QuestsService.HistoryTeam(arguments is { } requestData19 ? new HistoryTeamCommand(HistoryDefinitions: requestData19.HistoryDefinitions, HistoryQuery: requestData19.HistoryQuery) : null);
        };
        routes["get_clear_history"] = payload =>
        {
            if (payload is not null && payload is not ApiGetClearHistoryInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiGetClearHistoryInput;
            return QuestsService.ClearHistoryOverview(arguments?.HistoryQuery);
        };
        routes["get_clear_history_playerinfo"] = payload =>
        {
            if (payload is not null && payload is not ApiGetClearHistoryPlayerinfoInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiGetClearHistoryPlayerinfoInput;
            return QuestsService.HistoryPlayer(arguments is { } requestData20 ? new HistoryPlayerCommand(HistoryDefinitions: requestData20.HistoryDefinitions, Text: requestData20.Text) : null);
        };
        routes["get_history"] = payload =>
        {
            if (payload is not null && payload is not ApiGetHistoryInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiGetHistoryInput;
            return QuestsService.ProfileHistory();
        };
        routes["friend_get_history"] = payload =>
        {
            if (payload is not null && payload is not ApiFriendGetHistoryInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiFriendGetHistoryInput;
            return QuestsService.HistoryPlayer(arguments is { } requestData21 ? new HistoryPlayerCommand(HistoryDefinitions: requestData21.HistoryDefinitions, Text: requestData21.Text) : null, summaryOnly: true);
        };
        routes["tower_unit_recovery"] = payload =>
        {
            if (payload is not null && payload is not ApiTowerUnitRecoveryInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiTowerUnitRecoveryInput;
            return BattleModesService.RecoverTowerUnits(arguments is { } requestData22 ? new RecoverTowerUnitsCommand(Text: requestData22.Text) : null);
        };
        routes["mission_end"] = payload =>
        {
            if (payload is not null && payload is not ApiMissionEndInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiMissionEndInput;
            return MissionsService.ReceiveMission(arguments?.MissionClaim, arguments?.DailyLoginMission, arguments?.ProgressMissions, arguments?.DailyQuestMission);
        };
        routes["mission_top"] = payload =>
        {
            if (payload is not null && payload is not ApiMissionTopInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiMissionTopInput;
            return MissionsService.MissionOverview(arguments?.DailyLoginMission, arguments?.ProgressMissions, arguments?.DailyQuestMission, arguments?.BattlePassPeriod);
        };
        routes["battle_pass_mission_end"] = payload =>
        {
            if (payload is not null && payload is not ApiBattlePassMissionEndInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiBattlePassMissionEndInput;
            return MissionsService.ReceiveBattlePassMission(arguments?.BattlePassClaim, arguments?.BattlePassDaily);
        };
        routes["battle_pass_top"] = payload =>
        {
            if (payload is not null && payload is not ApiBattlePassTopInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiBattlePassTopInput;
            return MissionsService.BattlePassOverview(arguments?.BattlePassDaily);
        };
        routes["battle_pass_receive_reward"] = payload =>
        {
            if (payload is not null && payload is not ApiBattlePassReceiveRewardInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiBattlePassReceiveRewardInput;
            return MissionsService.ReceiveBattlePassRewards(arguments?.BattlePassRewards);
        };
        routes["get_series_battle_ranking_teaminfo"] = payload =>
        {
            if (payload is not null && payload is not ApiGetSeriesBattleRankingTeaminfoInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiGetSeriesBattleRankingTeaminfoInput;
            return QuestsService.RankingHistoryTeam(arguments is { } requestData23 ? new RankingHistoryTeamCommand(HistoryDefinitions: requestData23.HistoryDefinitions, Text: requestData23.Text, Value: requestData23.Value) : null);
        };
        routes["mini_game_top"] = payload =>
        {
            if (payload is not null && payload is not ApiMiniGameTopInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiMiniGameTopInput;
            return BattleModesService.MiniGameOverview(arguments is { } requestData24 ? new MiniGameOverviewCommand(Ids: requestData24.Ids) : null);
        };
        routes["pass_card/collect_exp"] = payload =>
        {
            if (payload is not null && payload is not ApiPassCardCollectExpInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiPassCardCollectExpInput;
            return ShopService.CollectPassExperience(arguments is { } requestData25 ? new CollectPassExperienceCommand(PassTask: requestData25.PassTask, Value: requestData25.Value) : null);
        };
        routes["pass_card/collect_reward"] = payload =>
        {
            if (payload is not null && payload is not ApiPassCardCollectRewardInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiPassCardCollectRewardInput;
            return ShopService.CollectPassRewards(arguments is { } requestData26 ? new CollectPassRewardsCommand(EntityId: requestData26.EntityId, PassRewards: requestData26.PassRewards, Text: requestData26.Text, Value: requestData26.Value) : null);
        };
        routes["pass_card/show_task"] = payload =>
        {
            if (payload is not null && payload is not ApiPassCardShowTaskInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiPassCardShowTaskInput;
            return ShopService.PassOverview();
        };
        routes["pass_card/show_shop"] = payload =>
        {
            if (payload is not null && payload is not ApiPassCardShowShopInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiPassCardShowShopInput;
            return ShopService.PassOverview();
        };
        routes["get_exchange_code_reward"] = payload =>
        {
            if (payload is not null && payload is not ApiGetExchangeCodeRewardInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiGetExchangeCodeRewardInput;
            return ShopService.ExchangeLocalCode(arguments is { } requestData27 ? new ExchangeLocalCodeCommand(CodeExchange: requestData27.CodeExchange, MailRules: requestData27.MailRules) : null);
        };
        routes["picture_book/get_reward"] = payload =>
        {
            if (payload is not null && payload is not ApiPictureBookGetRewardInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiPictureBookGetRewardInput;
            return ProfileService.ReceiveBookReward(arguments is { } requestData28 ? new ReceiveBookRewardCommand(BookReward: requestData28.BookReward, Value: requestData28.Value) : null);
        };
        routes["picture_book/get_user_picture_book_list"] = payload =>
        {
            if (payload is not null && payload is not ApiPictureBookGetUserPictureBookListInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiPictureBookGetUserPictureBookListInput;
            return new(0, new() { ["user_completed_info"] = PlayerState.user["_local_picture_book"]?.DeepClone() ?? new JsonArray() });
        };
        routes["dispatch_start"] = payload =>
        {
            if (payload is not null && payload is not ApiDispatchStartInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiDispatchStartInput;
            return DispatchService.StartDispatch(arguments is { } requestData29 ? new StartDispatchCommand(DispatchStart: requestData29.DispatchStart, EntityId: requestData29.EntityId, Ids: requestData29.Ids) : null);
        };
        routes["dispatch_end"] = payload =>
        {
            if (payload is not null && payload is not ApiDispatchEndInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiDispatchEndInput;
            return DispatchService.EndDispatch(arguments is { } requestData30 ? new EndDispatchCommand(DispatchSettlement: requestData30.DispatchSettlement, EntityId: requestData30.EntityId) : null);
        };
        routes["dispatch_bulk_end"] = payload =>
        {
            if (payload is not null && payload is not ApiDispatchBulkEndInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiDispatchBulkEndInput;
            return DispatchService.EndDispatchBatch();
        };
        routes["get_dispatch_list"] = payload =>
        {
            if (payload is not null && payload is not ApiGetDispatchListInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiGetDispatchListInput;
            return DispatchService.DispatchOverview();
        };
        routes["anniversary_info_update"] = payload =>
        {
            if (payload is not null && payload is not ApiAnniversaryInfoUpdateInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiAnniversaryInfoUpdateInput;
            return ProfileService.SaveAnniversaryInfo(arguments is { } requestData31 ? new SaveAnniversaryInfoCommand(Text: requestData31.Text, Value: requestData31.Value) : null);
        };
        routes["personal_info_update"] = payload =>
        {
            if (payload is not null && payload is not ApiPersonalInfoUpdateInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiPersonalInfoUpdateInput;
            return ProfileService.SaveContactDetails(arguments?.ContactDetails);
        };
        routes["personal_info_get"] = payload =>
        {
            if (payload is not null && payload is not ApiPersonalInfoGetInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiPersonalInfoGetInput;
            return new(0, PlayerState.user["_local_personal_info"]?.DeepClone().AsObject() ?? QueryCatalog.QueryTemplates["personal_info_get"].DeepClone().AsObject());
        };
        routes["anniversary_info_get"] = payload =>
        {
            if (payload is not null && payload is not ApiAnniversaryInfoGetInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiAnniversaryInfoGetInput;
            return new(0, PlayerState.user["_local_anniversary_info"]?.DeepClone().AsObject() ?? new JsonObject { ["mail_address"] = "" });
        };
        routes["auto_replay_start"] = payload =>
        {
            if (payload is not null && payload is not ApiAutoReplayStartInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiAutoReplayStartInput;
            return QuestsService.AutoReplayUnavailable(arguments is { } requestData32 ? new AutoReplayUnavailableCommand(ReplayMeasure: requestData32.ReplayMeasure, Value: requestData32.Value) : null);
        };
        routes["user_twitter_info_delete"] = payload =>
        {
            if (payload is not null && payload is not ApiUserTwitterInfoDeleteInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiUserTwitterInfoDeleteInput;
            return SocialService.DeleteLocalTwitterRelation(arguments?.Text);
        };
        routes["user_twitter_info_update"] = payload =>
        {
            if (payload is not null && payload is not ApiUserTwitterInfoUpdateInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiUserTwitterInfoUpdateInput;
            return SocialService.UpdateLocalTwitterRelation(arguments?.Text);
        };
        routes["user_apple_info_delete"] = payload =>
        {
            if (payload is not null && payload is not ApiUserAppleInfoDeleteInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiUserAppleInfoDeleteInput;
            return SocialService.ClearLocalRelation("apple_relation");
        };
        routes["user_google_info_delete"] = payload =>
        {
            if (payload is not null && payload is not ApiUserGoogleInfoDeleteInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiUserGoogleInfoDeleteInput;
            return SocialService.ClearLocalRelation("google_relation");
        };
        routes["lecture_room_unit_trust_up"] = payload =>
        {
            if (payload is not null && payload is not ApiLectureRoomUnitTrustUpInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiLectureRoomUnitTrustUpInput;
            return UnitsService.ReceiveLectureTrust(arguments is { } requestData33 ? new ReceiveLectureTrustCommand(EntityId: requestData33.EntityId, Value: requestData33.Value) : null);
        };
        routes["picture_recycle"] = payload =>
        {
            if (payload is not null && payload is not ApiPictureRecycleInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiPictureRecycleInput;
            return PicturesService.RecyclePictures(arguments is { } requestData34 ? new RecyclePicturesCommand(Ids: requestData34.Ids, PictureRecycle: requestData34.PictureRecycle) : null);
        };
        routes["unit_recycle"] = payload =>
        {
            if (payload is not null && payload is not ApiUnitRecycleInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiUnitRecycleInput;
            return UnitsService.RecycleUnits(arguments is { } requestData35 ? new RecycleUnitsCommand(Ids: requestData35.Ids, UnitRecycle: requestData35.UnitRecycle) : null);
        };
        routes["picture_potential_limitbreak"] = payload =>
        {
            if (payload is not null && payload is not ApiPicturePotentialLimitbreakInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiPicturePotentialLimitbreakInput;
            return PicturesService.LimitBreakPicture(arguments is { } requestData36 ? new LimitBreakPictureCommand(EntityId: requestData36.EntityId, Ids: requestData36.Ids, PictureLimitBreak: requestData36.PictureLimitBreak) : null);
        };
        routes["unit_destiny_value_up"] = payload =>
        {
            if (payload is not null && payload is not ApiUnitDestinyValueUpInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiUnitDestinyValueUpInput;
            return UnitsService.IncreaseDestiny(arguments is { } requestData37 ? new IncreaseDestinyCommand(DestinyGrowth: requestData37.DestinyGrowth, EntityId: requestData37.EntityId, Ids: requestData37.Ids) : null);
        };
        routes["unit_rank_promote_item_set"] = payload =>
        {
            if (payload is not null && payload is not ApiUnitRankPromoteItemSetInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiUnitRankPromoteItemSetInput;
            return UnitsService.SetRankMaterial(arguments is { } requestData38 ? new SetRankMaterialCommand(EntityId: requestData38.EntityId, Ids: requestData38.Ids, RankMaterial: requestData38.RankMaterial, Value: requestData38.Value) : null);
        };
        routes["unit_rank_promote"] = payload =>
        {
            if (payload is not null && payload is not ApiUnitRankPromoteInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiUnitRankPromoteInput;
            return UnitsService.PromoteRank(arguments is { } requestData39 ? new PromoteRankCommand(EntityId: requestData39.EntityId, RankPromotion: requestData39.RankPromotion) : null);
        };
        routes["unit_rare_awaken"] = payload =>
        {
            if (payload is not null && payload is not ApiUnitRareAwakenInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiUnitRareAwakenInput;
            return UnitsService.AwakenUnit(arguments is { } requestData40 ? new AwakenUnitCommand(Awakening: requestData40.Awakening, EntityId: requestData40.EntityId) : null);
        };
        routes["unit_level_limitbreak"] = payload =>
        {
            if (payload is not null && payload is not ApiUnitLevelLimitbreakInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiUnitLevelLimitbreakInput;
            return UnitsService.LimitBreakUnit(arguments is { } requestData41 ? new LimitBreakUnitCommand(EntityId: requestData41.EntityId, Ids: requestData41.Ids, LimitBreak: requestData41.LimitBreak, Value: requestData41.Value) : null);
        };
        routes["unit_spec_actualize"] = payload =>
        {
            if (payload is not null && payload is not ApiUnitSpecActualizeInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiUnitSpecActualizeInput;
            return UnitsService.StrengthenUnit(arguments is { } requestData42 ? new StrengthenUnitCommand(EntityId: requestData42.EntityId, StatMaterials: requestData42.StatMaterials) : null);
        };
        routes["unit_extra_spec_actualize"] = payload =>
        {
            if (payload is not null && payload is not ApiUnitExtraSpecActualizeInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiUnitExtraSpecActualizeInput;
            return UnitsService.StrengthenUnit(arguments is { } requestData43 ? new StrengthenUnitCommand(EntityId: requestData43.EntityId, StatMaterials: requestData43.StatMaterials) : null, true);
        };
        routes["limitbreak_strong_fragrantwood"] = payload =>
        {
            if (payload is not null && payload is not ApiLimitbreakStrongFragrantwoodInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiLimitbreakStrongFragrantwoodInput;
            return UnitsService.UnlockExtraStat(arguments is { } requestData44 ? new UnlockExtraStatCommand(EntityId: requestData44.EntityId, StatUnlock: requestData44.StatUnlock, Value: requestData44.Value) : null);
        };
        routes["picture_spec_actualize"] = payload =>
        {
            if (payload is not null && payload is not ApiPictureSpecActualizeInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiPictureSpecActualizeInput;
            return PicturesService.StrengthenPicture(arguments is { } requestData45 ? new StrengthenPictureCommand(EntityId: requestData45.EntityId, PictureGrowth: requestData45.PictureGrowth) : null);
        };
        routes["picture_spec_actualize_chemicals"] = payload =>
        {
            if (payload is not null && payload is not ApiPictureSpecActualizeChemicalsInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiPictureSpecActualizeChemicalsInput;
            return PicturesService.StrengthenPicture(arguments is { } requestData45 ? new StrengthenPictureCommand(EntityId: requestData45.EntityId, PictureGrowth: requestData45.PictureGrowth) : null);
        };
        routes["phantasm_point_buy"] = payload =>
        {
            if (payload is not null && payload is not ApiPhantasmPointBuyInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiPhantasmPointBuyInput;
            return ShopService.BuyPoints(arguments?.PointPurchase);
        };
        routes["gather_gold"] = payload =>
        {
            if (payload is not null && payload is not ApiGatherGoldInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiGatherGoldInput;
            return FacilitiesService.GatherGold(arguments is { } requestData46 ? new GatherGoldCommand(EntityId: requestData46.EntityId, GoldIncome: requestData46.GoldIncome, Value: requestData46.Value) : null);
        };
        routes["gold_box_renovation_end"] = payload =>
        {
            if (payload is not null && payload is not ApiGoldBoxRenovationEndInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiGoldBoxRenovationEndInput;
            return FacilitiesService.CompleteBoxRenovation("gold_box", arguments is { } requestData47 ? new CompleteBoxRenovationCommand(EntityId: requestData47.EntityId, GoldIncome: requestData47.GoldIncome, SpiritIncome: requestData47.SpiritIncome, Value: requestData47.Value) : null);
        };
        routes["phantasm_box_renovation_end"] = payload =>
        {
            if (payload is not null && payload is not ApiPhantasmBoxRenovationEndInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiPhantasmBoxRenovationEndInput;
            return FacilitiesService.CompleteBoxRenovation("phantasm_box", arguments is { } requestData48 ? new CompleteBoxRenovationCommand(EntityId: requestData48.EntityId, GoldIncome: requestData48.GoldIncome, SpiritIncome: requestData48.SpiritIncome, Value: requestData48.Value) : null);
        };
        routes["phantasm_box_renovation_start"] = payload =>
        {
            if (payload is not null && payload is not ApiPhantasmBoxRenovationStartInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiPhantasmBoxRenovationStartInput;
            return FacilitiesService.StartBoxRenovation("phantasm_box", arguments?.Renovation);
        };
        routes["gold_box_renovation_start"] = payload =>
        {
            if (payload is not null && payload is not ApiGoldBoxRenovationStartInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiGoldBoxRenovationStartInput;
            return FacilitiesService.StartBoxRenovation("gold_box", arguments?.Renovation);
        };
        routes["gather_phantasm_point"] = payload =>
        {
            if (payload is not null && payload is not ApiGatherPhantasmPointInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiGatherPhantasmPointInput;
            return FacilitiesService.GatherPoints(arguments is { } requestData49 ? new GatherPointsCommand(EntityId: requestData49.EntityId, SpiritIncome: requestData49.SpiritIncome, Value: requestData49.Value) : null);
        };
        routes["friend/receive"] = payload =>
        {
            if (payload is not null && payload is not ApiFriendReceiveInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiFriendReceiveInput;
            return SocialService.ReceiveContactGift(arguments is { } requestData50 ? new ReceiveContactGiftCommand(EntityId: requestData50.EntityId, Text: requestData50.Text, Value: requestData50.Value) : null);
        };
        routes["delete_account"] = payload =>
        {
            if (payload is not null && payload is not ApiDeleteAccountInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiDeleteAccountInput;
            return Accounts.DeleteAccount();
        };
        routes["tower_top"] = payload =>
        {
            if (payload is not null && payload is not ApiTowerTopInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiTowerTopInput;
            return BattleModesService.TowerOverview();
        };
        routes["get_tower_unit_info"] = payload =>
        {
            if (payload is not null && payload is not ApiGetTowerUnitInfoInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiGetTowerUnitInfoInput;
            return BattleModesService.TowerUnits();
        };
        routes["mail_receive"] = payload =>
        {
            if (payload is not null && payload is not ApiMailReceiveInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiMailReceiveInput;
            return MailService.ReceiveMail(arguments is { } requestData51 ? new ReceiveMailCommand(Ids: requestData51.Ids, MailRules: requestData51.MailRules) : null);
        };
        routes["friend_helper_select"] = payload =>
        {
            if (payload is not null && payload is not ApiFriendHelperSelectInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiFriendHelperSelectInput;
            return SocialService.HelperCandidates(arguments?.Value);
        };
        routes["get_quest_cleared_team_info"] = payload =>
        {
            if (payload is not null && payload is not ApiGetQuestClearedTeamInfoInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiGetQuestClearedTeamInfoInput;
            return TeamsService.ClearedTeam(arguments?.ReplayMeasure);
        };
        routes["afk_set"] = payload =>
        {
            if (payload is not null && payload is not ApiAfkSetInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiAfkSetInput;
            return FacilitiesService.SetIdleSlot(arguments?.Idle);
        };
        routes["afk_complete_upgrade"] = payload =>
        {
            if (payload is not null && payload is not ApiAfkCompleteUpgradeInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiAfkCompleteUpgradeInput;
            return FacilitiesService.CompleteIdleSlot(arguments is { } requestData52 ? new CompleteIdleSlotCommand(EntityId: requestData52.EntityId, Idle: requestData52.Idle, Value: requestData52.Value) : null);
        };
        routes["afk_receive_drop"] = payload =>
        {
            if (payload is not null && payload is not ApiAfkReceiveDropInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiAfkReceiveDropInput;
            return FacilitiesService.ReceiveIdleDrop();
        };
        routes["episode_end"] = payload =>
        {
            if (payload is not null && payload is not ApiEpisodeEndInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiEpisodeEndInput;
            return QuestsService.CompleteEpisode(arguments is { } requestData53 ? new CompleteEpisodeCommand(EntityId: requestData53.EntityId, Value: requestData53.Value) : null);
        };
        routes["event_top"] = payload =>
        {
            if (payload is not null && payload is not ApiEventTopInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiEventTopInput;
            return EventsService.EventOverview();
        };
        routes["daily_arena_top"] = payload =>
        {
            if (payload is not null && payload is not ApiDailyArenaTopInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiDailyArenaTopInput;
            return BattleModesService.DailyOverview();
        };
        routes["weekly_arena_top"] = payload =>
        {
            if (payload is not null && payload is not ApiWeeklyArenaTopInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiWeeklyArenaTopInput;
            return BattleModesService.WeeklyOverview();
        };
        routes["weekly_arena_battle_score_reset"] = payload =>
        {
            if (payload is not null && payload is not ApiWeeklyArenaBattleScoreResetInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiWeeklyArenaBattleScoreResetInput;
            return BattleModesService.ResetWeeklyScore(arguments?.Value);
        };
        routes["friend_request"] = payload =>
        {
            if (payload is not null && payload is not ApiFriendRequestInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiFriendRequestInput;
            return SocialService.SendContactRequest(arguments is { } requestData54 ? new SendContactRequestCommand(EntityId: requestData54.EntityId, Text: requestData54.Text, Value: requestData54.Value) : null);
        };
        routes["friend_get_candidate_list"] = payload =>
        {
            if (payload is not null && payload is not ApiFriendGetCandidateListInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiFriendGetCandidateListInput;
            return new(0, new() { ["helper_list"] = PlayerState.user["_local_friend_candidates"]?.DeepClone() ?? new JsonArray() });
        };
        routes["friend_approve"] = payload =>
        {
            if (payload is not null && payload is not ApiFriendApproveInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiFriendApproveInput;
            return SocialService.ApproveContactRequest(arguments is { } requestData55 ? new ApproveContactRequestCommand(Text: requestData55.Text, Value: requestData55.Value) : null);
        };
        routes["friend_request_cancel"] = payload =>
        {
            if (payload is not null && payload is not ApiFriendRequestCancelInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiFriendRequestCancelInput;
            return SocialService.CancelContactRequest(arguments is { } requestData56 ? new CancelContactRequestCommand(Text: requestData56.Text, Value: requestData56.Value) : null);
        };
        routes["friend_delete"] = payload =>
        {
            if (payload is not null && payload is not ApiFriendDeleteInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiFriendDeleteInput;
            return SocialService.DeleteContact(arguments?.Text);
        };
        routes["reset_point_allocation"] = payload =>
        {
            if (payload is not null && payload is not ApiResetPointAllocationInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiResetPointAllocationInput;
            return UnitsService.ResetPoints(arguments?.PointReset);
        };
        routes["allocate_points"] = payload =>
        {
            if (payload is not null && payload is not ApiAllocatePointsInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiAllocatePointsInput;
            return UnitsService.AllocatePoints(arguments?.PointAllocation);
        };
        routes["friend/apply"] = payload =>
        {
            if (payload is not null && payload is not ApiFriendApplyInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiFriendApplyInput;
            return SocialService.ApplyContact(arguments is { } requestData57 ? new ApplyContactCommand(Text: requestData57.Text, Value: requestData57.Value) : null);
        };
        routes["friend/recommend"] = payload =>
        {
            if (payload is not null && payload is not ApiFriendRecommendInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiFriendRecommendInput;
            return new(0, new() { ["data"] = PlayerState.user["_local_friend_recommendations"]?.DeepClone() ?? new JsonArray() });
        };
        routes["friend/info"] = payload =>
        {
            if (payload is not null && payload is not ApiFriendInfoInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiFriendInfoInput;
            return SocialService.ContactInfo(arguments?.Text);
        };
        routes["friend/refuse_apply"] = payload =>
        {
            if (payload is not null && payload is not ApiFriendRefuseApplyInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiFriendRefuseApplyInput;
            return SocialService.RefuseContactApplications(arguments is { } requestData58 ? new RefuseContactApplicationsCommand(Text: requestData58.Text, Value: requestData58.Value) : null);
        };
        routes["friend/accept_apply"] = payload =>
        {
            if (payload is not null && payload is not ApiFriendAcceptApplyInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiFriendAcceptApplyInput;
            return SocialService.AcceptContactApplication(arguments is { } requestData59 ? new AcceptContactApplicationCommand(Text: requestData59.Text, Value: requestData59.Value) : null);
        };
        routes["friend/apply_list"] = payload =>
        {
            if (payload is not null && payload is not ApiFriendApplyListInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiFriendApplyListInput;
            return new(0, new() { ["data"] = PlayerState.user["_local_friend_applications"]?.DeepClone() ?? new JsonArray() });
        };
        routes["friend/list"] = payload =>
        {
            if (payload is not null && payload is not ApiFriendListInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiFriendListInput;
            return new(0, new() { ["data"] = PlayerState.user["_local_friends"]?.DeepClone() ?? new JsonArray(), ["request_tips"] = PlayerState.user["_local_friend_applications"] is JsonArray { Count: > 0 } ? 1 : 0 });
        };
        routes["avatar/list"] = payload =>
        {
            if (payload is not null && payload is not ApiAvatarListInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiAvatarListInput;
            return new(0, new() { ["avatar_list"] = PlayerState.user["HeadListItemList"]?.DeepClone() ?? new JsonArray() });
        };
        routes["avatar/preview"] = payload =>
        {
            if (payload is not null && payload is not ApiAvatarPreviewInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiAvatarPreviewInput;
            return ProfileService.PreviewAvatar(arguments?.Value);
        };
        routes["avatar/update"] = payload =>
        {
            if (payload is not null && payload is not ApiAvatarUpdateInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiAvatarUpdateInput;
            return ProfileService.SelectAvatar(arguments?.Value);
        };
        routes["training_room_start"] = payload =>
        {
            if (payload is not null && payload is not ApiTrainingRoomStartInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiTrainingRoomStartInput;
            return FacilitiesService.StartTraining(arguments?.Training);
        };
        routes["training_room_shortened_finish_time"] = payload =>
        {
            if (payload is not null && payload is not ApiTrainingRoomShortenedFinishTimeInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiTrainingRoomShortenedFinishTimeInput;
            return FacilitiesService.ShortenTraining(arguments is { } requestData60 ? new ShortenTrainingCommand(EntityId: requestData60.EntityId, TrainingMaterials: requestData60.TrainingMaterials) : null);
        };
        routes["lecture_room_food_distribution"] = payload =>
        {
            if (payload is not null && payload is not ApiLectureRoomFoodDistributionInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiLectureRoomFoodDistributionInput;
            return FacilitiesService.DistributeFood(arguments?.FoodDistribution);
        };
        routes["lecture_room_food_addition"] = payload =>
        {
            if (payload is not null && payload is not ApiLectureRoomFoodAdditionInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiLectureRoomFoodAdditionInput;
            return FacilitiesService.PurchaseFood(arguments?.FoodPurchase);
        };
        routes["series_battle_team_update"] = payload =>
        {
            if (payload is not null && payload is not ApiSeriesBattleTeamUpdateInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiSeriesBattleTeamUpdateInput;
            return TeamsService.UpdateSeriesTeams(arguments?.SeriesTeams);
        };
        routes["series_battle_reset"] = payload =>
        {
            if (payload is not null && payload is not ApiSeriesBattleResetInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiSeriesBattleResetInput;
            return BattleModesService.ResetSeries();
        };
        routes["tower_battle_start"] = payload =>
        {
            if (payload is not null && payload is not ApiTowerBattleStartInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiTowerBattleStartInput;
            return BattleModesService.StartTower(arguments?.TowerStart);
        };
        routes["tower_battle_end"] = payload =>
        {
            if (payload is not null && payload is not ApiTowerBattleEndInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiTowerBattleEndInput;
            return BattleModesService.EndTower(arguments?.TowerEnd);
        };
        routes["series_battle_start"] = payload =>
        {
            if (payload is not null && payload is not ApiSeriesBattleStartInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiSeriesBattleStartInput;
            return BattleModesService.StartSeries(arguments?.SeriesStart);
        };
        routes["series_battle_end"] = payload =>
        {
            if (payload is not null && payload is not ApiSeriesBattleEndInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiSeriesBattleEndInput;
            return BattleModesService.EndSeries(arguments?.SeriesEnd);
        };
        routes["comic_get_pages"] = payload =>
        {
            if (payload is not null && payload is not ApiComicGetPagesInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiComicGetPagesInput;
            return ProfileService.AcquirePages(arguments?.Pages);
        };
        routes["comic_read_end"] = payload =>
        {
            if (payload is not null && payload is not ApiComicReadEndInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiComicReadEndInput;
            return ProfileService.ReadPages(arguments is { } requestData61 ? new ReadPagesCommand(EntityId: requestData61.EntityId, Value: requestData61.Value) : null);
        };
        routes["roguelite_top"] = payload =>
        {
            if (payload is not null && payload is not ApiRogueliteTopInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiRogueliteTopInput;
            return TeamsService.DeckOverview(arguments?.Value);
        };
        routes["roguelite_deck_save"] = payload =>
        {
            if (payload is not null && payload is not ApiRogueliteDeckSaveInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiRogueliteDeckSaveInput;
            return TeamsService.SaveDeck(arguments is { } requestData62 ? new SaveDeckCommand(Ids: requestData62.Ids, Text: requestData62.Text, Value: requestData62.Value) : null);
        };
        routes["roguelite_deck_call"] = payload =>
        {
            if (payload is not null && payload is not ApiRogueliteDeckCallInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiRogueliteDeckCallInput;
            return TeamsService.CallDeck(arguments?.Value);
        };
        routes["rhythm_game_start"] = payload =>
        {
            if (payload is not null && payload is not ApiRhythmGameStartInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiRhythmGameStartInput;
            return BattleModesService.StartRhythm(arguments is { } requestData63 ? new StartRhythmCommand(EntityId: requestData63.EntityId, Value: requestData63.Value) : null);
        };
        routes["achievement_select"] = payload =>
        {
            if (payload is not null && payload is not ApiAchievementSelectInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiAchievementSelectInput;
            return ProfileService.SelectAchievement(arguments?.Value);
        };
        routes["achievement_top"] = payload =>
        {
            if (payload is not null && payload is not ApiAchievementTopInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiAchievementTopInput;
            return ProfileService.AchievementOverview();
        };
        routes["rhythm_game_end"] = payload =>
        {
            if (payload is not null && payload is not ApiRhythmGameEndInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiRhythmGameEndInput;
            return BattleModesService.EndRhythm(arguments?.Rhythm);
        };
        routes["rhythm_game_top"] = payload =>
        {
            if (payload is not null && payload is not ApiRhythmGameTopInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiRhythmGameTopInput;
            return new(0, new() { ["clear_info"] = PlayerState.user["_local_rhythm_scores"]?.DeepClone() ?? new JsonArray() });
        };
        routes["tutorial_update"] = payload =>
        {
            if (payload is not null && payload is not ApiTutorialUpdateInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiTutorialUpdateInput;
            return ProfileService.UpdateProgress(arguments?.Value);
        };
        routes["read_unit_talk"] = payload =>
        {
            if (payload is not null && payload is not ApiReadUnitTalkInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiReadUnitTalkInput;
            return ProfileService.MarkTalkRead(arguments?.Value);
        };
        routes["mail_delete"] = payload =>
        {
            if (payload is not null && payload is not ApiMailDeleteInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiMailDeleteInput;
            return MailService.DeleteReadMail();
        };
        routes["unit_voice_update"] = payload =>
        {
            if (payload is not null && payload is not ApiUnitVoiceUpdateInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiUnitVoiceUpdateInput;
            return ProfileService.SetVoice(arguments is { } requestData64 ? new SetVoiceCommand(EntityId: requestData64.EntityId, Value: requestData64.Value) : null);
        };
        routes["lecture_room_unit_remove"] = payload =>
        {
            if (payload is not null && payload is not ApiLectureRoomUnitRemoveInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiLectureRoomUnitRemoveInput;
            return FacilitiesService.RemoveLectureUnit(arguments?.EntityId);
        };
        routes["lecture_room_unit_set"] = payload =>
        {
            if (payload is not null && payload is not ApiLectureRoomUnitSetInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiLectureRoomUnitSetInput;
            return FacilitiesService.AddLectureUnit(arguments is { } requestData65 ? new AddLectureUnitCommand(EntityId: requestData65.EntityId, Value: requestData65.Value) : null);
        };
        routes["lecture_room_unit_expansion"] = payload =>
        {
            if (payload is not null && payload is not ApiLectureRoomUnitExpansionInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiLectureRoomUnitExpansionInput;
            return FacilitiesService.ExpandFacility(arguments is { } requestData66 ? new ExpandFacilityCommand(EntityId: requestData66.EntityId, Value: requestData66.Value) : null, "lecture_room", "unit");
        };
        routes["lecture_room_food_expansion"] = payload =>
        {
            if (payload is not null && payload is not ApiLectureRoomFoodExpansionInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiLectureRoomFoodExpansionInput;
            return FacilitiesService.ExpandFacility(arguments is { } requestData67 ? new ExpandFacilityCommand(EntityId: requestData67.EntityId, Value: requestData67.Value) : null, "lecture_room", "food");
        };
        routes["training_room_expansion"] = payload =>
        {
            if (payload is not null && payload is not ApiTrainingRoomExpansionInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiTrainingRoomExpansionInput;
            return FacilitiesService.ExpandFacility(arguments is { } requestData68 ? new ExpandFacilityCommand(EntityId: requestData68.EntityId, Value: requestData68.Value) : null, "training_room");
        };
        routes["training_room_cancel"] = payload =>
        {
            if (payload is not null && payload is not ApiTrainingRoomCancelInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiTrainingRoomCancelInput;
            return FacilitiesService.CancelTraining(arguments?.Value);
        };
        routes["dispatch_cancel"] = payload =>
        {
            if (payload is not null && payload is not ApiDispatchCancelInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiDispatchCancelInput;
            return FacilitiesService.CancelDispatch(arguments?.EntityId);
        };
        routes["unit_equip_picture_reset"] = payload =>
        {
            if (payload is not null && payload is not ApiUnitEquipPictureResetInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiUnitEquipPictureResetInput;
            return PicturesService.ResetEquipment(arguments?.EntityId);
        };
        routes["unit_equip_picture"] = payload =>
        {
            if (payload is not null && payload is not ApiUnitEquipPictureInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiUnitEquipPictureInput;
            return PicturesService.SetEquipment(arguments is { } requestData69 ? new SetEquipmentCommand(EntityId: requestData69.EntityId, Equipment: requestData69.Equipment) : null);
        };
        routes["unit_equip_costume"] = payload =>
        {
            if (payload is not null && payload is not ApiUnitEquipCostumeInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiUnitEquipCostumeInput;
            return PicturesService.SetCostumes(arguments?.Costumes);
        };
        routes["training_room_update"] = payload =>
        {
            if (payload is not null && payload is not ApiTrainingRoomUpdateInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiTrainingRoomUpdateInput;
            return new(0, new() { ["user_update"] = new JsonObject { ["training_room"] = PlayerState.user["training_room"]!.DeepClone() } });
        };
        routes["lecture_room_update"] = payload =>
        {
            if (payload is not null && payload is not ApiLectureRoomUpdateInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiLectureRoomUpdateInput;
            return new(0, new() { ["spend_info"] = new JsonObject { ["passage_time"] = 0, ["food"] = 0, ["exp"] = 0 }, ["user_update"] = new JsonObject { ["lecture_room"] = PlayerState.user["lecture_room"]!.DeepClone() } });
        };
        routes["transcribe_kpi"] = payload =>
        {
            if (payload is not null && payload is not ApiTranscribeKpiInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiTranscribeKpiInput;
            return arguments?.Value is 1 or 2 ? new(0, new()) : new(400, null, "Unknown local telemetry event.");
        };
        routes["favorite_unit_update"] = payload =>
        {
            if (payload is not null && payload is not ApiFavoriteUnitUpdateInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiFavoriteUnitUpdateInput;
            return ProfileService.UpdateHome(arguments?.Home);
        };
        routes["home_select_unit_update"] = payload =>
        {
            if (payload is not null && payload is not ApiHomeSelectUnitUpdateInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiHomeSelectUnitUpdateInput;
            return ProfileService.SelectHomeUnit(arguments?.Value);
        };
        routes["unit_bgm_update"] = payload =>
        {
            if (payload is not null && payload is not ApiUnitBgmUpdateInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiUnitBgmUpdateInput;
            return ProfileService.SetUnitPreference(arguments is { } requestData70 ? new SetUnitPreferenceCommand(EntityId: requestData70.EntityId, Value: requestData70.Value) : null, "select_bgm", minimum: ProfileService.NoBgm);
        };
        routes["update_home_motion_mode"] = payload =>
        {
            if (payload is not null && payload is not ApiUpdateHomeMotionModeInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiUpdateHomeMotionModeInput;
            return ProfileService.SetUnitPreference(arguments is { } requestData71 ? new SetUnitPreferenceCommand(EntityId: requestData71.EntityId, Value: requestData71.Value) : null, "home_motion");
        };
        routes["home_bgm_update"] = payload =>
        {
            if (payload is not null && payload is not ApiHomeBgmUpdateInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiHomeBgmUpdateInput;
            return arguments?.Value is >= ProfileService.NoBgm ? ProfileService.SetUserInteger("select_bgm", arguments.Value.Value) : new(400, null, "Nonnegative music selection required.");
        };
        routes["team_update"] = payload =>
        {
            if (payload is not null && payload is not ApiTeamUpdateInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiTeamUpdateInput;
            return TeamsService.UpdateTeam(arguments?.Team);
        };
        routes["friend_setting_helper"] = payload =>
        {
            if (payload is not null && payload is not ApiFriendSettingHelperInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiFriendSettingHelperInput;
            return SocialService.SetHelper(arguments is { } requestData72 ? new SetHelperCommand(EntityId: requestData72.EntityId, Value: requestData72.Value) : null);
        };
        routes["friend_top"] = payload =>
        {
            if (payload is not null && payload is not ApiFriendTopInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiFriendTopInput;
            return SocialService.ContactOverview();
        };
        routes["achievement_new_update"] = payload =>
        {
            if (payload is not null && payload is not ApiAchievementNewUpdateInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiAchievementNewUpdateInput;
            return ProfileService.ClearAchievementMarkers(arguments?.Ids);
        };
        routes["get_replay_info"] = payload =>
        {
            if (payload is not null && payload is not ApiGetReplayInfoInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiGetReplayInfoInput;
            return QuestsService.QuestReplay(arguments is { } requestData73 ? new QuestReplayCommand(ReplayMeasure: requestData73.ReplayMeasure, Text: requestData73.Text) : null, false);
        };
        routes["update_replay_info"] = payload =>
        {
            if (payload is not null && payload is not ApiUpdateReplayInfoInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiUpdateReplayInfoInput;
            return QuestsService.QuestReplay(arguments is { } requestData74 ? new QuestReplayCommand(ReplayMeasure: requestData74.ReplayMeasure, Text: requestData74.Text) : null, true);
        };
        routes["get_series_battle_replay_info"] = payload =>
        {
            if (payload is not null && payload is not ApiGetSeriesBattleReplayInfoInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiGetSeriesBattleReplayInfoInput;
            return QuestsService.GetSeriesReplays(arguments is { } requestData75 ? new GetSeriesReplaysCommand(EntityId: requestData75.EntityId, Ids: requestData75.Ids) : null);
        };
        routes["update_series_battle_replay_info"] = payload =>
        {
            if (payload is not null && payload is not ApiUpdateSeriesBattleReplayInfoInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiUpdateSeriesBattleReplayInfoInput;
            return QuestsService.SaveSeriesReplay(arguments is { } requestData76 ? new SaveSeriesReplayCommand(EntityId: requestData76.EntityId, Text: requestData76.Text, Value: requestData76.Value) : null);
        };
        routes["get_auto_replay_info"] = payload =>
        {
            if (payload is not null && payload is not ApiGetAutoReplayInfoInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiGetAutoReplayInfoInput;
            return QuestsService.ReplayMeasurement(arguments?.ReplayMeasure, false);
        };
        routes["get_multiple_spirits_weekly_replay_info"] = payload =>
        {
            if (payload is not null && payload is not ApiGetMultipleSpiritsWeeklyReplayInfoInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiGetMultipleSpiritsWeeklyReplayInfoInput;
            return QuestsService.GetStoredReplay(arguments?.EntityId);
        };
        routes["update_multiple_spirits_weekly_replay_info"] = payload =>
        {
            if (payload is not null && payload is not ApiUpdateMultipleSpiritsWeeklyReplayInfoInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiUpdateMultipleSpiritsWeeklyReplayInfoInput;
            return QuestsService.SaveReplay(arguments?.ReplayContent);
        };
        routes["update_auto_replay_info"] = payload =>
        {
            if (payload is not null && payload is not ApiUpdateAutoReplayInfoInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiUpdateAutoReplayInfoInput;
            return QuestsService.ReplayMeasurement(arguments?.ReplayMeasure, true);
        };
        routes["update_profile_setting"] = payload =>
        {
            if (payload is not null && payload is not ApiUpdateProfileSettingInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiUpdateProfileSettingInput;
            return ProfileService.SaveProfileLayout(arguments?.Text);
        };
        routes["update_profile_setting2"] = payload =>
        {
            if (payload is not null && payload is not ApiUpdateProfileSetting2Input) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiUpdateProfileSetting2Input;
            return new(0, new() { ["text_value"] = arguments?.Text is null || arguments.Text.Any(char.IsControl) ? 1 : 0 });
        };
        routes["get_profile_setting"] = payload =>
        {
            if (payload is not null && payload is not ApiGetProfileSettingInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiGetProfileSettingInput;
            return new(0, new() { ["profile_setting"] = PlayerState.user["_local_profile_setting"]?.GetValue<string>() ?? "", ["achievement_id_list"] = new JsonArray() });
        };
        routes["user_info_update"] = payload =>
        {
            if (payload is not null && payload is not ApiUserInfoUpdateInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiUserInfoUpdateInput;
            return ProfileService.UpdateProfile(arguments?.Profile);
        };
        routes["friend/search"] = payload =>
        {
            if (payload is not null && payload is not ApiFriendSearchInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiFriendSearchInput;
            return new(2000, null, "No matching local contact.");
        };
        routes["set_age"] = payload =>
        {
            if (payload is not null && payload is not ApiSetAgeInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiSetAgeInput;
            return arguments?.Value is >= 1 and <= 3 ? ProfileService.SetUserInteger("age_limit_type", arguments.Value.Value) : new(400, null, "Age category must be 1, 2 or 3.");
        };
        routes["logout"] = payload =>
        {
            if (payload is not null && payload is not ApiLogoutInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiLogoutInput;
            return arguments?.Text == SessionState.account!.UserId ? new(0, new()) : new(403, null, "Local identity does not match.");
        };
        routes["unit_protection"] = payload =>
        {
            if (payload is not null && payload is not ApiUnitProtectionInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiUnitProtectionInput;
            return flags.SetProtection(arguments is { } requestData78 ? new SetProtectionCommand(EntityId: requestData78.EntityId, Value: requestData78.Value) : null, "unit", "unit_list", "user_unit_id");
        };
        routes["picture_protection"] = payload =>
        {
            if (payload is not null && payload is not ApiPictureProtectionInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiPictureProtectionInput;
            return flags.SetProtection(arguments is { } requestData79 ? new SetProtectionCommand(EntityId: requestData79.EntityId, Value: requestData79.Value) : null, "picture", "picture_list", "user_picture_id");
        };
        routes["unit_new_update"] = payload =>
        {
            if (payload is not null && payload is not ApiUnitNewUpdateInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiUnitNewUpdateInput;
            return flags.ClearNewFlags(arguments is { } requestData80 ? new ClearNewFlagsCommand(Ids: requestData80.Ids) : null, "unit", "unit_list", "user_unit_id");
        };
        routes["picture_new_update"] = payload =>
        {
            if (payload is not null && payload is not ApiPictureNewUpdateInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiPictureNewUpdateInput;
            return flags.ClearNewFlags(arguments is { } requestData81 ? new ClearNewFlagsCommand(Ids: requestData81.Ids) : null, "picture", "picture_list", "user_picture_id");
        };
        routes["unit_voice_autoset_update"] = payload =>
        {
            if (payload is not null && payload is not ApiUnitVoiceAutosetUpdateInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiUnitVoiceAutosetUpdateInput;
            return flags.SetVoiceAutoset(arguments?.Value);
        };
        routes["battle_start"] = payload =>
        {
            if (payload is not null && payload is not ApiBattleStartInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiBattleStartInput;
            return QuestsService.StartQuestBattle(arguments?.QuestBattleStart);
        };
        routes["battle_end"] = payload =>
        {
            if (payload is not null && payload is not ApiBattleEndInput) return new(400, null, "Request payload does not match API.");
            var arguments = payload as ApiBattleEndInput;
            return QuestsService.EndQuestBattle(arguments?.QuestBattleEnd, arguments?.DailyQuestDay);
        };
        return routes;
    }

}
