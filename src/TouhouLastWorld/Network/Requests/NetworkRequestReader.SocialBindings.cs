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
    private static RequestDraft? ReadUserTwitterInfoUpdateArguments(RequestPacket request)
    {
        var api = "user_twitter_info_update";
        RequestDraft? arguments = null;
        if (api == "user_twitter_info_update")
            arguments = new RequestDraft(Text: request.TryCast<UserTwitterInfoUpdateRequest>()?.twitter_user_id);

        return arguments;
    }
    private static RequestDraft? ReadUserTwitterInfoDeleteArguments(RequestPacket request)
    {
        var api = "user_twitter_info_delete";
        RequestDraft? arguments = null;
        if (api == "user_twitter_info_delete")
            arguments = new RequestDraft(Text: request.TryCast<UserTwitterInfoDeleteRequest>()?.twitter_user_id);

        return arguments;
    }
    private static RequestDraft? ReadFriendHelperSelectArguments(RequestPacket request)
    {
        var api = "friend_helper_select";
        RequestDraft? arguments = null;
        if (api == "friend_helper_select")
            arguments = new RequestDraft(Value: MasterDB.This()?.Setting?.FRIEND_COUNT_MAX);

        return arguments;
    }
    private static RequestDraft? ReadFriendApplyArguments(RequestPacket request)
    {
        var api = "friend/apply";
        RequestDraft? arguments = null;
        if (api == "friend/apply")
            arguments = new RequestDraft(Value: MasterDB.This()?.Setting?.FRIEND_COUNT_MAX,
                Text: request.TryCast<FriendApplyRequest>()?.public_id);

        return arguments;
    }
    private static RequestDraft? ReadFriendRequestArguments(RequestPacket request)
    {
        var api = "friend_request";
        RequestDraft? arguments = null;
        if (api == "friend_request")
            arguments = new RequestDraft(EntityId: MasterDB.This()?.Setting?.FRIEND_COUNT_MAX,
                Value: MasterDB.This()?.Setting?.REQUEST_COUNT_MAX, Text: request.TryCast<FriendRequestRequest>()?.request_player_id);

        return arguments;
    }
    private static RequestDraft? ReadFriendApproveArguments(RequestPacket request)
    {
        var api = "friend_approve";
        RequestDraft? arguments = null;
        if (api == "friend_approve")
            arguments = new RequestDraft(Value: MasterDB.This()?.Setting?.FRIEND_COUNT_MAX,
                Text: request.TryCast<FriendApproveRequest>()?.approve_player_id);

        return arguments;
    }
    private static RequestDraft? ReadFriendRequestCancelArguments(RequestPacket request)
    {
        var api = "friend_request_cancel";
        RequestDraft? arguments = null;
        if (api == "friend_request_cancel")
        {
            var cancellation = request.TryCast<FriendRequestCancelRequest>();
            arguments = cancellation == null ? null : new RequestDraft(Value: cancellation.is_delete_all, Text: cancellation.cancel_player_id);
        }

        return arguments;
    }
    private static RequestDraft? ReadFriendDeleteArguments(RequestPacket request)
    {
        var api = "friend_delete";
        RequestDraft? arguments = null;
        if (api == "friend_delete")
            arguments = new RequestDraft(Text: request.TryCast<FriendDeleteRequest>()?.delete_player_id);

        return arguments;
    }
    private static RequestDraft? ReadFriendInfoArguments(RequestPacket request)
    {
        var api = "friend/info";
        RequestDraft? arguments = null;
        if (api == "friend/info")
            arguments = new RequestDraft(Text: request.TryCast<FriendInfoRequest>()?.public_id);

        return arguments;
    }
    private static RequestDraft? ReadFriendGiveArguments(RequestPacket request)
    {
        var api = "friend/give";
        RequestDraft? arguments = null;
        if (api == "friend/give")
        {
            var gift = request.TryCast<FriendGiveRequest>();
            arguments = gift == null ? null : new RequestDraft(Value: gift.is_all ? 1 : 0, Text: gift.public_id);
        }

        return arguments;
    }
    private static RequestDraft? ReadFriendReceiveArguments(RequestPacket request)
    {
        var api = "friend/receive";
        RequestDraft? arguments = null;
        if (api == "friend/receive")
        {
            var gift = request.TryCast<FriendGetRequest>();
            arguments = gift == null ? null : new RequestDraft(MasterDB.This()?.Setting?.GOLD_MAX,
                gift.is_all ? 1 : 0, Text: gift.public_id);
        }

        return arguments;
    }
    private static RequestDraft? ReadFriendAcceptApplyArguments(RequestPacket request)
    {
        var api = "friend/accept_apply";
        RequestDraft? arguments = null;
        if (api == "friend/accept_apply")
            arguments = new RequestDraft(Value: MasterDB.This()?.Setting?.FRIEND_COUNT_MAX,
                Text: request.TryCast<FriendAcceptRequest>()?.public_id);

        return arguments;
    }
    private static RequestDraft? ReadFriendRefuseApplyArguments(RequestPacket request)
    {
        var api = "friend/refuse_apply";
        RequestDraft? arguments = null;
        if (api == "friend/refuse_apply")
        {
            var refusal = request.TryCast<FriendRefuseRequest>();
            arguments = refusal == null ? null : new RequestDraft(Value: refusal.is_all ? 1 : 0, Text: refusal.public_id);
        }

        return arguments;
    }
    private static RequestDraft? ReadFriendSettingHelperArguments(RequestPacket request)
    {
        var api = "friend_setting_helper";
        RequestDraft? arguments = null;
        if (api == "friend_setting_helper")
        {
            var update = request.TryCast<FriendSettingHelperRequest>();
            arguments = update == null ? null : new RequestDraft(update.user_unit_id1, update.total_power);
        }

        return arguments;
    }
}
