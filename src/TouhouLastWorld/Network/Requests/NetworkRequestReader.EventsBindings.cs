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
    private static RequestDraft? ReadFudaiFudaiInfoArguments(RequestPacket request)
    {
        var api = "fudai/fudai_info";
        RequestDraft? arguments = null;
        if (api == "fudai/fudai_info" && request.TryCast<TreasureInfoRequest>() is not null)
            arguments = new RequestDraft(Value: MasterDB.This()?.Event?.ToList()?.ToArray().FirstOrDefault(row => row.event_type == 99)?.id);

        return arguments;
    }
    private static RequestDraft? ReadFudaiFudaiDrawArguments(RequestPacket request)
    {
        var api = "fudai/fudai_draw";
        RequestDraft? arguments = null;
        if (api == "fudai/fudai_draw" && request.TryCast<TreasureGachaRequest>() is not null)
        {
            var master = MasterDB.This();
            var row = master?.Event?.ToList()?.ToArray().FirstOrDefault(entry => entry.event_type == 99);
            var gifts = master?.FuDaiGiftWeight?.ToList()?.ToArray();
            var time = GameSystem.This()?.Time;
            if (row != null && gifts != null && time != null)
                arguments = new RequestDraft(TreasureDraw: new(row.id,
                    time.IsActiveDate(row.start_at, row.end_at), gifts.Select(gift => gift.id).ToArray()));
        }

        return arguments;
    }
    private static RequestDraft? ReadEventPointGachaDoArguments(RequestPacket request)
    {
        var api = "event_point_gacha_do";
        RequestDraft? arguments = null;
        if (api == "event_point_gacha_do") arguments = ReadEventDraw(request.TryCast<EventPointGachaDoAPIRequest>());

        return arguments;
    }
    private static RequestDraft? ReadGetEventTotalPointRewardArguments(RequestPacket request)
    {
        var api = "get_event_total_point_reward";
        RequestDraft? arguments = null;
        if (api == "get_event_total_point_reward")
            arguments = ReadEventRewards(request.TryCast<GetEventTotalPointRewardRequest>()?.event_id);

        return arguments;
    }
    private static RequestDraft? ReadGetEventTotalPointRewardAllArguments(RequestPacket request)
    {
        var api = "get_event_total_point_reward_all";
        RequestDraft? arguments = null;
        if (api == "get_event_total_point_reward_all")
            arguments = ReadEventRewards(request.TryCast<GetEventTotalPointRewardAllRequest>()?.event_id);

        return arguments;
    }
}
