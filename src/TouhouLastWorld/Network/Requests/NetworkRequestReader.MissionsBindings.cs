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
    private static RequestDraft? ReadMissionTopArguments(RequestPacket request)
    {
        var api = "mission_top";
        RequestDraft? arguments = null;
        if (api == "mission_top") arguments = ReadMissionOverview();

        return arguments;
    }
    private static RequestDraft? ReadMissionEndArguments(RequestPacket request)
    {
        var api = "mission_end";
        RequestDraft? arguments = null;
        if (api == "mission_end") arguments = ReadMissionClaim(request.TryCast<MissionEndRequest>());

        return arguments;
    }
    private static RequestDraft? ReadBattlePassMissionEndArguments(RequestPacket request)
    {
        var api = "battle_pass_mission_end";
        RequestDraft? arguments = null;
        if (api == "battle_pass_mission_end") arguments = ReadBattlePassClaim(request.TryCast<BattlePassMissionEndRequest>());

        return arguments;
    }
    private static RequestDraft? ReadBattlePassTopArguments(RequestPacket request)
    {
        var api = "battle_pass_top";
        RequestDraft? arguments = null;
        if (api == "battle_pass_top") arguments = ReadBattlePassDailyOverview();

        return arguments;
    }
    private static RequestDraft? ReadBattlePassReceiveRewardArguments(RequestPacket request)
    {
        var api = "battle_pass_receive_reward";
        RequestDraft? arguments = null;
        if (api == "battle_pass_receive_reward") arguments = ReadBattlePassRewards();

        return arguments;
    }
}
