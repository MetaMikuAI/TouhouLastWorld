using System.Collections;
using System.IO;
using System.IO.Compression;

using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;

using static TouhouLastWorld.Patches.HarmonyPrefixFlow;

using NativeRoutine = Il2CppSystem.Collections.IEnumerator;

namespace TouhouLastWorld.Patches;

[HarmonyPatch(typeof(global::AssetLoader))]
[AutoLog]
public partial class AssetLoaderPatch
{
    [HarmonyPatch(nameof(AssetLoader.DownloadManifest), typeof(string), typeof(string), typeof(int))]
    [HarmonyPrefix]
    public static bool DownloadManifest_Prefix(AssetLoader __instance, string remotePath, string manifestPath,
        ref NativeRoutine __result)
    {
        var root = UnityEngine.Application.streamingAssetsPath.TrimEnd('/');
        var entry = remotePath switch
        {
            var path when path == root + "/assetbundle/android" => "assets/assetbundle/android/manifest.json",
            var path when path == root + "/master" => "assets/master/manifest.json",
            var path when path == root + "/resource" => "assets/resource/manifest.json",
            _ => null
        };
        if (entry is null) return RunOriginal;
        __result = ReadApkManifest(__instance, remotePath, manifestPath, entry).WrapToIl2Cpp();
        return SkipOriginal;
    }

    private static IEnumerator ReadApkManifest(AssetLoader loader, string remotePath, string manifestPath, string entry)
    {
        string text;
        using (var apk = ZipFile.OpenRead(UnityEngine.Application.dataPath))
        using (var stream = apk.GetEntry(entry)!.Open())
        using (var reader = new StreamReader(stream))
            text = reader.ReadToEnd();
        Log.Info($"APK manifest loaded: {entry}");
        yield return Cysharp.Threading.Tasks.UniTaskExtensions.ToCoroutine(loader.m_manifest.AppendTextAsync(text), null);
        loader.m_updateManifestInfoList.Add(new AssetLoader.UpdateManifestInfo
        {
            remotePath = remotePath, manifestPath = manifestPath, manifestText = text
        });
    }

    [HarmonyPatch(nameof(AssetLoader.InitializeAsync))]
    [HarmonyPostfix]
    public static void InitializeAsync_Postfix(ref NativeRoutine __result)
    {
        __result = Observe(__result).WrapToIl2Cpp();
    }

    private static IEnumerator Observe(NativeRoutine original)
    {
        Log.Info("Resource initialization started.");
        var state = original.TryCast<AssetLoader._InitializeAsync_d__39>();
        var lastState = int.MinValue;
        while (original.MoveNext())
        {
            if (state is not null && state.__1__state != lastState)
            {
                lastState = state.__1__state;
                Log.Info($"Resource initialization stage: {lastState}");
            }
            yield return original.Current;
        }
        Log.Info("Resource initialization completed.");
    }
}
