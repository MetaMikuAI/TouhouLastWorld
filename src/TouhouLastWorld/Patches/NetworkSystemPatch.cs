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
        if (api == "logout")
            arguments = new LocalArguments(Text: request.TryCast<LogoutRequest>()?.user_id);
        if (api == "set_age")
            arguments = new LocalArguments(Value: request.TryCast<SetAgeRequest>()?.age_type);
        if (api == "read_unit_talk")
            arguments = new LocalArguments(Value: request.TryCast<ReadUnitTalkRequest>()?.talk_id);
        if (api == "training_room_cancel")
            arguments = new LocalArguments(Value: request.TryCast<TrainingRoomCancelRequest>()?.slot_number);
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
