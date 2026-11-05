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
    private static RequestDraft? ReadPictureBookGetRewardArguments(RequestPacket request)
    {
        var api = "picture_book/get_reward";
        RequestDraft? arguments = null;
        if (api == "picture_book/get_reward")
        {
            var change = request.TryCast<PictureBookReciveRequest>();
            var master = MasterDB.This();
            var row = change == null ? null : master?.PictureBookTask?.Find(change.task_id);
            arguments = new RequestDraft(Value: change?.task_id);
            if (row != null && master?.Setting != null)
            {
                var capacity = row.reward_object_type switch
                {
                    3 => master.Setting.GOLD_MAX,
                    6 => master.Setting.CRYSTAL_MAX,
                    12 when master.Item?.Find(row.reward_object_id) != null => master.Setting.ITEM_MAX,
                    _ => 0
                };
                arguments = arguments with
                {
                    BookReward = new(row.id, row.picture_book_id,
                    new(row.reward_object_type, row.reward_object_id, row.reward_object_value, capacity))
                };
            }
        }

        return arguments;
    }
    private static RequestDraft? ReadPersonalInfoUpdateArguments(RequestPacket request)
    {
        var api = "personal_info_update";
        RequestDraft? arguments = null;
        if (api == "personal_info_update")
        {
            var change = request.TryCast<EventPersonalInfoUpdateAPIRequest>();
            if (change != null) arguments = new RequestDraft(ContactDetails: new(change.user_name, change.zip_code, change.address, change.phone_number));
        }

        return arguments;
    }
    private static RequestDraft? ReadAnniversaryInfoUpdateArguments(RequestPacket request)
    {
        var api = "anniversary_info_update";
        RequestDraft? arguments = null;
        if (api == "anniversary_info_update")
        {
            var change = request.TryCast<EventAnniversaryInfoUpdateAPIRequest>();
            arguments = new RequestDraft(Value: change?.is_agree, Text: change?.mail_address);
        }

        return arguments;
    }
    private static RequestDraft? ReadAvatarPreviewArguments(RequestPacket request)
    {
        var api = "avatar/preview";
        RequestDraft? arguments = null;
        if (api == "avatar/preview")
            arguments = new RequestDraft(Value: request.TryCast<previewAvatarAPIAsyncRequest>()?.avatar_id);

        return arguments;
    }
    private static RequestDraft? ReadAvatarUpdateArguments(RequestPacket request)
    {
        var api = "avatar/update";
        RequestDraft? arguments = null;
        if (api == "avatar/update")
            arguments = new RequestDraft(Value: request.TryCast<replaceAvatarRequest>()?.avatar_id);

        return arguments;
    }
    private static RequestDraft? ReadComicGetPagesArguments(RequestPacket request)
    {
        var api = "comic_get_pages";
        RequestDraft? arguments = null;
        if (api == "comic_get_pages")
        {
            var pages = request.TryCast<ComicGetPagesRequest>();
            var definition = pages == null ? null : MasterDB.This()?.Comic?.Find(pages.comic_id);
            var price = MasterDB.This()?.Setting?.GET_COMIC_PAGE_USED_POINT;
            arguments = pages == null || definition == null || price == null ? null
                : new RequestDraft(Pages: new(pages.comic_id, pages.page_count, definition.max_page_num, price.Value));
        }

        return arguments;
    }
    private static RequestDraft? ReadComicReadEndArguments(RequestPacket request)
    {
        var api = "comic_read_end";
        RequestDraft? arguments = null;
        if (api == "comic_read_end")
        {
            var pages = request.TryCast<ComicReadEndRequest>();
            arguments = pages == null ? null : new RequestDraft(pages.comic_id, pages.page_number);
        }

        return arguments;
    }
    private static RequestDraft? ReadAchievementSelectArguments(RequestPacket request)
    {
        var api = "achievement_select";
        RequestDraft? arguments = null;
        if (api == "achievement_select")
            arguments = new RequestDraft(Value: request.TryCast<AchievementSelectRequest>()?.achievement_id);

        return arguments;
    }
    private static RequestDraft? ReadLogoutArguments(RequestPacket request)
    {
        var api = "logout";
        RequestDraft? arguments = null;
        if (api == "logout")
            arguments = new RequestDraft(Text: request.TryCast<LogoutRequest>()?.user_id);

        return arguments;
    }
    private static RequestDraft? ReadSetAgeArguments(RequestPacket request)
    {
        var api = "set_age";
        RequestDraft? arguments = null;
        if (api == "set_age")
            arguments = new RequestDraft(Value: request.TryCast<SetAgeRequest>()?.age_type);

        return arguments;
    }
    private static RequestDraft? ReadReadUnitTalkArguments(RequestPacket request)
    {
        var api = "read_unit_talk";
        RequestDraft? arguments = null;
        if (api == "read_unit_talk")
            arguments = new RequestDraft(Value: request.TryCast<ReadUnitTalkRequest>()?.talk_id);

        return arguments;
    }
    private static RequestDraft? ReadTranscribeKpiArguments(RequestPacket request)
    {
        var api = "transcribe_kpi";
        RequestDraft? arguments = null;
        if (api == "transcribe_kpi")
            arguments = new RequestDraft(Value: request.TryCast<TranscribeKpiRequest>()?.transcribe_value);

        return arguments;
    }
    private static RequestDraft? ReadUnitVoiceUpdateArguments(RequestPacket request)
    {
        var api = "unit_voice_update";
        RequestDraft? arguments = null;
        if (api == "unit_voice_update")
        {
            var update = request.TryCast<UnitVoiceUpdateRequest>();
            arguments = update == null ? null : new RequestDraft(update.unit_id, update.voice_set_id);
        }

        return arguments;
    }
    private static RequestDraft? ReadHomeSelectUnitUpdateArguments(RequestPacket request)
    {
        var api = "home_select_unit_update";
        RequestDraft? arguments = null;
        if (api == "home_select_unit_update")
            arguments = new RequestDraft(Value: request.TryCast<HomeCustomSelectUnitUpdateRequest>()?.select_unit_index);

        return arguments;
    }
    private static RequestDraft? ReadFavoriteUnitUpdateArguments(RequestPacket request)
    {
        var api = "favorite_unit_update";
        RequestDraft? arguments = null;
        if (api == "favorite_unit_update")
        {
            var update = request.TryCast<HomeCustomSetUpdateRequest>();
            arguments = update == null ? null : new RequestDraft(Home: new(update.unit_param?.ToArray()
                .Select(entry => entry == null ? new LocalHomeEntry(0, 0, 0, 0, 0)
                    : new LocalHomeEntry(entry.unit_id, entry.user_unit_id, entry.costume_id, entry.home_motion, entry.bgm)).ToArray(),
                update.select_unit_index, update.random_select_mode));
        }

        return arguments;
    }
    private static RequestDraft? ReadHomeBgmUpdateArguments(RequestPacket request)
    {
        var api = "home_bgm_update";
        RequestDraft? arguments = null;
        if (api == "home_bgm_update")
            arguments = new RequestDraft(Value: request.TryCast<HomeBGMUpdateRequest>()?.select_bgm_id);

        return arguments;
    }
    private static RequestDraft? ReadUpdateHomeMotionModeArguments(RequestPacket request)
    {
        var api = "update_home_motion_mode";
        RequestDraft? arguments = null;
        if (api == "update_home_motion_mode")
        {
            var update = request.TryCast<UpdateHomeMotionModeRequest>();
            arguments = update == null ? null : new RequestDraft(update.user_unit_id, update.mode);
        }

        return arguments;
    }
    private static RequestDraft? ReadUnitBgmUpdateArguments(RequestPacket request)
    {
        var api = "unit_bgm_update";
        RequestDraft? arguments = null;
        if (api == "unit_bgm_update")
        {
            var update = request.TryCast<UnitBGMUpdateRequest>();
            arguments = update == null ? null : new RequestDraft(update.user_unit_id, update.select_bgm_id);
        }

        return arguments;
    }
    private static RequestDraft? ReadAchievementNewUpdateArguments(RequestPacket request)
    {
        var api = "achievement_new_update";
        RequestDraft? arguments = null;
        if (api == "achievement_new_update")
            arguments = new RequestDraft(Ids: request.TryCast<AchievementNewUpdateRequest>()?.new_update_id_list?.ToArray()
                .Select(id => (long)id).ToArray());

        return arguments;
    }
    private static RequestDraft? ReadUpdateProfileSettingArguments(RequestPacket request)
    {
        var api = "update_profile_setting";
        RequestDraft? arguments = null;
        if (api == "update_profile_setting")
            arguments = new RequestDraft(Text: request.TryCast<UpdateProfileSettingAPIRequest>()?.profile_setting);

        return arguments;
    }
    private static RequestDraft? ReadUpdateProfileSetting2Arguments(RequestPacket request)
    {
        var api = "update_profile_setting2";
        RequestDraft? arguments = null;
        if (api == "update_profile_setting2")
            arguments = new RequestDraft(Text: request.TryCast<UpdateProfileSetting2APIRequest>()?.text_string);

        return arguments;
    }
    private static RequestDraft? ReadUserInfoUpdateArguments(RequestPacket request)
    {
        var api = "user_info_update";
        RequestDraft? arguments = null;
        if (api == "user_info_update")
        {
            var names = request.TryCast<Comic.UserInfoUpdateRequest>();
            arguments = new RequestDraft(Profile: new LocalProfileUpdate(
                names?.player_name ?? request.TryCast<UserPlayerNameUpdateRequest>()?.player_name,
                names?.hero_name ?? request.TryCast<UserHeroNameUpdateRequest>()?.hero_name,
                request.TryCast<UserCommentUpdateRequest>()?.comment));
        }

        return arguments;
    }
    private static RequestDraft? ReadTutorialUpdateArguments(RequestPacket request)
    {
        var api = "tutorial_update";
        RequestDraft? arguments = null;
        if (api == "tutorial_update")
            arguments = new RequestDraft(Value: request.TryCast<TutorialUpdateRequest>()?.progress_value);

        return arguments;
    }
    private static RequestDraft? ReadUnitVoiceAutosetUpdateArguments(RequestPacket request)
    {
        var api = "unit_voice_autoset_update";
        RequestDraft? arguments = null;
        if (api == "unit_voice_autoset_update")
            arguments = new RequestDraft(Value: request.TryCast<UnitVoiceAutosetUpdateRequest>()?.autoset_flag);

        return arguments;
    }
    private static RequestDraft? ReadPictureProtectionArguments(RequestPacket request)
    {
        var api = "picture_protection";
        RequestDraft? arguments = null;
        if (api == "picture_protection")
        {
            var update = request.TryCast<PictureProtectionRequest>();
            arguments = update == null ? null : new RequestDraft(update.user_picture_id, update.is_protection);
        }

        return arguments;
    }
    private static RequestDraft? ReadUnitNewUpdateArguments(RequestPacket request)
    {
        var api = "unit_new_update";
        RequestDraft? arguments = null;
        if (api == "unit_new_update")
        {
            var update = request.TryCast<UnitNewUpdateRequest>();
            arguments = new RequestDraft(Ids: update?.user_unit_id_list?.ToArray());
        }

        return arguments;
    }
    private static RequestDraft? ReadPictureNewUpdateArguments(RequestPacket request)
    {
        var api = "picture_new_update";
        RequestDraft? arguments = null;
        if (api == "picture_new_update")
        {
            var update = request.TryCast<PictureNewUpdateRequest>();
            arguments = new RequestDraft(Ids: update?.user_picture_id_list?.ToArray());
        }

        return arguments;
    }
}
