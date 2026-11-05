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
    private static RequestDraft? ReadDispatchEndArguments(RequestPacket request)
    {
        var api = "dispatch_end";
        RequestDraft? arguments = null;
        if (api == "dispatch_end") arguments = ReadDispatchSettlement(request.TryCast<DispatchEndRequest>());

        return arguments;
    }
    private static RequestDraft? ReadDispatchStartArguments(RequestPacket request)
    {
        var api = "dispatch_start";
        RequestDraft? arguments = null;
        if (api == "dispatch_start") arguments = ReadDispatchStart(request.TryCast<DispatchStartRequest>());

        return arguments;
    }
}
