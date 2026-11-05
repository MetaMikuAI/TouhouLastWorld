using System;
using System.Collections;
using System.Globalization;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Applet;
using static TouhouLastWorld.Patches.HarmonyPrefixFlow;
using static TouhouLastWorld.Patches.ActivePanels;
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
    public static bool TransceiveRoutine_Prefix(NetworkSystem __instance, RequestPacket request, ResponsePacket response, PacketCallback action, ref NativeRoutine __result)
    {
        __result = Respond(__instance, request, response, action).WrapToIl2Cpp();
        return SkipOriginal;
    }

    private static IEnumerator Respond(NetworkSystem network, RequestPacket request, ResponsePacket response, PacketCallback action)
    {
        yield return null;
        var api = request.GetAPIName();
        var input = NetworkRequestReader.Read(request, api);
        var reply = Plugin.Server.Dispatch(input);
        Log.Info($"Offline API: {api}; status={reply.Status}");
        network.m_recv.status = reply.Status;
        network.m_recv.now = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        network.m_recv.error_message = reply.Error;
        network.m_recv.body = "";
        if (reply.Status != 0)
        {
            NetworkErrorRecovery.Handle(api, reply, response, action);
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
