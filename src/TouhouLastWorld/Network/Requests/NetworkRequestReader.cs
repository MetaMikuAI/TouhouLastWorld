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
    private static readonly Dictionary<string, Func<RequestPacket, RequestDraft?>> Readers = new(StringComparer.Ordinal)
    {
        ["battle_start"] = ReadBattleStartArguments,
        ["battle_end"] = ReadBattleEndArguments,
        ["get_clear_history"] = ReadGetClearHistoryArguments,
        ["unit_recycle"] = ReadUnitRecycleArguments,
        ["get_clear_history_playerinfo"] = ReadGetClearHistoryPlayerinfoArguments,
        ["friend_get_history"] = ReadFriendGetHistoryArguments,
        ["tower_unit_recovery"] = ReadTowerUnitRecoveryArguments,
        ["mission_top"] = ReadMissionTopArguments,
        ["mission_end"] = ReadMissionEndArguments,
        ["battle_pass_mission_end"] = ReadBattlePassMissionEndArguments,
        ["battle_pass_top"] = ReadBattlePassTopArguments,
        ["battle_pass_receive_reward"] = ReadBattlePassReceiveRewardArguments,
        ["get_series_battle_ranking_teaminfo"] = ReadGetSeriesBattleRankingTeaminfoArguments,
        ["mini_game_top"] = ReadMiniGameTopArguments,
        ["multiple_spirits_weekly_top"] = ReadMultipleSpiritsWeeklyTopArguments,
        ["multiple_spirits_weekly_battle_start"] = ReadMultipleSpiritsWeeklyBattleStartArguments,
        ["roguelite_reroll"] = ReadRogueliteRerollArguments,
        ["batch_search"] = ReadBatchSearchArguments,
        ["multiple_spirits_weekly_battle_end"] = ReadMultipleSpiritsWeeklyBattleEndArguments,
        ["arena_battle_end"] = ReadArenaBattleEndArguments,
        ["arena_battle_start"] = ReadArenaBattleStartArguments,
        ["tower_battle_end"] = ReadTowerBattleEndArguments,
        ["tower_battle_start"] = ReadTowerBattleStartArguments,
        ["series_battle_end"] = ReadSeriesBattleEndArguments,
        ["series_battle_start"] = ReadSeriesBattleStartArguments,
        ["dispatch_end"] = ReadDispatchEndArguments,
        ["gacha_point_exchange"] = ReadGachaPointExchangeArguments,
        ["retry_gacha_fix"] = ReadRetryGachaFixArguments,
        ["get_clear_history_teaminfo"] = ReadGetClearHistoryTeaminfoArguments,
        ["first_draw_save"] = ReadFirstDrawSaveArguments,
        ["first_draw_data"] = ReadFirstDrawDataArguments,
        ["exchange"] = ReadExchangeArguments,
        ["update_gold_exchange_shop_list"] = ReadUpdateGoldExchangeShopListArguments,
        ["unit_reincarnation_release"] = ReadUnitReincarnationReleaseArguments,
        ["votive_picture_exchange"] = ReadVotivePictureExchangeArguments,
        ["fudai/fudai_info"] = ReadFudaiFudaiInfoArguments,
        ["fudai/fudai_draw"] = ReadFudaiFudaiDrawArguments,
        ["event_point_gacha_do"] = ReadEventPointGachaDoArguments,
        ["get_event_total_point_reward"] = ReadGetEventTotalPointRewardArguments,
        ["get_event_total_point_reward_all"] = ReadGetEventTotalPointRewardAllArguments,
        ["gain_free_item"] = ReadGainFreeItemArguments,
        ["pass_card/buy_goods"] = ReadPassCardBuyGoodsArguments,
        ["pass_card/user_orb_add_level"] = ReadPassCardUserOrbAddLevelArguments,
        ["pass_card/get_level_gift"] = ReadPassCardGetLevelGiftArguments,
        ["pass_card/show_reward"] = ReadPassCardShowRewardArguments,
        ["pass_card/collect_reward"] = ReadPassCardCollectRewardArguments,
        ["pass_card/collect_exp"] = ReadPassCardCollectExpArguments,
        ["get_exchange_code_reward"] = ReadGetExchangeCodeRewardArguments,
        ["picture_book/get_reward"] = ReadPictureBookGetRewardArguments,
        ["dispatch_start"] = ReadDispatchStartArguments,
        ["personal_info_update"] = ReadPersonalInfoUpdateArguments,
        ["anniversary_info_update"] = ReadAnniversaryInfoUpdateArguments,
        ["item_exchange"] = ReadItemExchangeArguments,
        ["auto_replay_start"] = ReadAutoReplayStartArguments,
        ["user_twitter_info_update"] = ReadUserTwitterInfoUpdateArguments,
        ["user_twitter_info_delete"] = ReadUserTwitterInfoDeleteArguments,
        ["lecture_room_unit_trust_up"] = ReadLectureRoomUnitTrustUpArguments,
        ["picture_recycle"] = ReadPictureRecycleArguments,
        ["picture_potential_limitbreak"] = ReadPicturePotentialLimitbreakArguments,
        ["unit_rank_promote_item_set"] = ReadUnitRankPromoteItemSetArguments,
        ["unit_rank_promote"] = ReadUnitRankPromoteArguments,
        ["unit_spec_actualize"] = ReadUnitSpecActualizeArguments,
        ["unit_extra_spec_actualize"] = ReadUnitExtraSpecActualizeArguments,
        ["limitbreak_strong_fragrantwood"] = ReadLimitbreakStrongFragrantwoodArguments,
        ["picture_spec_actualize"] = ReadPictureSpecActualizeArguments,
        ["picture_spec_actualize_chemicals"] = ReadPictureSpecActualizeChemicalsArguments,
        ["unit_destiny_value_up"] = ReadUnitDestinyValueUpArguments,
        ["unit_level_limitbreak"] = ReadUnitLevelLimitbreakArguments,
        ["unit_rare_awaken"] = ReadUnitRareAwakenArguments,
        ["mail_receive"] = ReadMailReceiveArguments,
        ["friend_helper_select"] = ReadFriendHelperSelectArguments,
        ["phantasm_point_buy"] = ReadPhantasmPointBuyArguments,
        ["gather_gold"] = ReadGatherGoldArguments,
        ["gold_box_renovation_start"] = ReadGoldBoxRenovationStartArguments,
        ["gold_box_renovation_end"] = ReadGoldBoxRenovationEndArguments,
        ["gather_phantasm_point"] = ReadGatherPhantasmPointArguments,
        ["phantasm_box_renovation_start"] = ReadPhantasmBoxRenovationStartArguments,
        ["phantasm_box_renovation_end"] = ReadPhantasmBoxRenovationEndArguments,
        ["get_quest_cleared_team_info"] = ReadGetQuestClearedTeamInfoArguments,
        ["afk_set"] = ReadAfkSetArguments,
        ["afk_complete_upgrade"] = ReadAfkCompleteUpgradeArguments,
        ["allocate_points"] = ReadAllocatePointsArguments,
        ["reset_point_allocation"] = ReadResetPointAllocationArguments,
        ["weekly_arena_battle_score_reset"] = ReadWeeklyArenaBattleScoreResetArguments,
        ["episode_end"] = ReadEpisodeEndArguments,
        ["friend/apply"] = ReadFriendApplyArguments,
        ["friend_request"] = ReadFriendRequestArguments,
        ["friend_approve"] = ReadFriendApproveArguments,
        ["friend_request_cancel"] = ReadFriendRequestCancelArguments,
        ["friend_delete"] = ReadFriendDeleteArguments,
        ["friend/info"] = ReadFriendInfoArguments,
        ["friend/give"] = ReadFriendGiveArguments,
        ["friend/receive"] = ReadFriendReceiveArguments,
        ["friend/accept_apply"] = ReadFriendAcceptApplyArguments,
        ["friend/refuse_apply"] = ReadFriendRefuseApplyArguments,
        ["avatar/preview"] = ReadAvatarPreviewArguments,
        ["avatar/update"] = ReadAvatarUpdateArguments,
        ["training_room_shortened_finish_time"] = ReadTrainingRoomShortenedFinishTimeArguments,
        ["training_room_start"] = ReadTrainingRoomStartArguments,
        ["lecture_room_food_distribution"] = ReadLectureRoomFoodDistributionArguments,
        ["lecture_room_food_addition"] = ReadLectureRoomFoodAdditionArguments,
        ["series_battle_team_update"] = ReadSeriesBattleTeamUpdateArguments,
        ["comic_get_pages"] = ReadComicGetPagesArguments,
        ["comic_read_end"] = ReadComicReadEndArguments,
        ["roguelite_top"] = ReadRogueliteTopArguments,
        ["roguelite_deck_call"] = ReadRogueliteDeckCallArguments,
        ["roguelite_deck_save"] = ReadRogueliteDeckSaveArguments,
        ["achievement_select"] = ReadAchievementSelectArguments,
        ["rhythm_game_start"] = ReadRhythmGameStartArguments,
        ["rhythm_game_end"] = ReadRhythmGameEndArguments,
        ["logout"] = ReadLogoutArguments,
        ["set_age"] = ReadSetAgeArguments,
        ["read_unit_talk"] = ReadReadUnitTalkArguments,
        ["training_room_cancel"] = ReadTrainingRoomCancelArguments,
        ["dispatch_cancel"] = ReadDispatchCancelArguments,
        ["unit_equip_picture_reset"] = ReadUnitEquipPictureResetArguments,
        ["unit_equip_costume"] = ReadUnitEquipCostumeArguments,
        ["unit_equip_picture"] = ReadUnitEquipPictureArguments,
        ["lecture_room_unit_remove"] = ReadLectureRoomUnitRemoveArguments,
        ["lecture_room_unit_set"] = ReadLectureRoomUnitSetArguments,
        ["lecture_room_unit_expansion"] = ReadLectureRoomUnitExpansionArguments,
        ["lecture_room_food_expansion"] = ReadLectureRoomFoodExpansionArguments,
        ["training_room_expansion"] = ReadTrainingRoomExpansionArguments,
        ["transcribe_kpi"] = ReadTranscribeKpiArguments,
        ["unit_voice_update"] = ReadUnitVoiceUpdateArguments,
        ["home_select_unit_update"] = ReadHomeSelectUnitUpdateArguments,
        ["favorite_unit_update"] = ReadFavoriteUnitUpdateArguments,
        ["home_bgm_update"] = ReadHomeBgmUpdateArguments,
        ["update_home_motion_mode"] = ReadUpdateHomeMotionModeArguments,
        ["unit_bgm_update"] = ReadUnitBgmUpdateArguments,
        ["friend_setting_helper"] = ReadFriendSettingHelperArguments,
        ["team_update"] = ReadTeamUpdateArguments,
        ["achievement_new_update"] = ReadAchievementNewUpdateArguments,
        ["get_replay_info"] = ReadGetReplayInfoArguments,
        ["update_replay_info"] = ReadUpdateReplayInfoArguments,
        ["get_series_battle_replay_info"] = ReadGetSeriesBattleReplayInfoArguments,
        ["update_series_battle_replay_info"] = ReadUpdateSeriesBattleReplayInfoArguments,
        ["get_auto_replay_info"] = ReadGetAutoReplayInfoArguments,
        ["get_multiple_spirits_weekly_replay_info"] = ReadGetMultipleSpiritsWeeklyReplayInfoArguments,
        ["update_multiple_spirits_weekly_replay_info"] = ReadUpdateMultipleSpiritsWeeklyReplayInfoArguments,
        ["update_auto_replay_info"] = ReadUpdateAutoReplayInfoArguments,
        ["update_profile_setting"] = ReadUpdateProfileSettingArguments,
        ["update_profile_setting2"] = ReadUpdateProfileSetting2Arguments,
        ["user_info_update"] = ReadUserInfoUpdateArguments,
        ["tutorial_update"] = ReadTutorialUpdateArguments,
        ["unit_voice_autoset_update"] = ReadUnitVoiceAutosetUpdateArguments,
        ["picture_protection"] = ReadPictureProtectionArguments,
        ["unit_new_update"] = ReadUnitNewUpdateArguments,
        ["picture_new_update"] = ReadPictureNewUpdateArguments,
        ["unit_protection"] = request => request.TryCast<UnitProtectionRequest>() is { } value ? new(value.user_unit_id, value.is_protection) : null
    };
    public static GameRequest Read(RequestPacket request, string api)
    {
        var gacha = api == "gacha_do" ? request.TryCast<GachaDoRequest>() : null;
        var arguments = Readers.TryGetValue(api, out var read) ? read(request) : null;
        if (api is "home" or "gather_gold" or "gather_phantasm_point" or "gold_box_renovation_end" or "phantasm_box_renovation_end")
            arguments = ReadFacilityIncome(arguments ?? new());

        if (api is "home" or "login" or "battle_end" || arguments?.QuestBattleStart?.Daily is not null)
            arguments = (arguments ?? new()) with { DailyQuestDay = ReadDailyQuestDay() };

        if (api == "home")
            arguments = (arguments ?? new()) with { ProgressMissions = ReadHomeNoviceMissions() };

        return RequestAdapter.Create(api, gacha?.count, gacha?.gacha_id, gacha?.gacha_button_id, arguments);
    }
}
