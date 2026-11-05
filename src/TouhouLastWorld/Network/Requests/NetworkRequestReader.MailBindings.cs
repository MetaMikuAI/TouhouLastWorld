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
    private static RequestDraft? ReadMailReceiveArguments(RequestPacket request)
    {
        var api = "mail_receive";
        RequestDraft? arguments = null;
        if (api == "mail_receive")
        {
            var setting = MasterDB.This()?.Setting;
            var items = MasterDB.This()?.Item?.ToList()?.ToArray().Select(item => item.id).ToArray();
            arguments = new RequestDraft(Ids: request.TryCast<MailReceiveRequest>()?.receive_id_list?.ToArray(),
                MailRules: setting == null ? null : new(setting.GOLD_MAX, setting.CRYSTAL_MAX, setting.ITEM_MAX, items));
        }

        return arguments;
    }
}
