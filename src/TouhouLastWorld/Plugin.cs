using System.IO;

using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

using TouhouLastWorld.Patches;

namespace TouhouLastWorld;

[BepInPlugin(Id, "TouhouLastWorld", "0.6.0")]
public sealed class Plugin : BasePlugin
{
    public const string Id = "local.touhoulastworld";
    public static Plugin Instance { get; private set; } = null!;
    public static OfflineServer Server { get; private set; } = null!;
    private readonly Harmony harmony = new(Id);

    public override void Load()
    {
        Instance = this;
        if (!Config.Bind("General", "Enabled", true, "启用进程内游戏服务；重启生效。SDK 保持原样。").Value)
            return;
        var resourceRoot = Config.Bind("Offline", "ResourceRoot", "offline://resources/", "默认使用 APK 内置资源；重启生效。").Value;
        if (resourceRoot == "offline://resources/")
            resourceRoot = UnityEngine.Application.streamingAssetsPath;
        using var stream = typeof(Plugin).Assembly.GetManifestResourceStream("TouhouLastWorld.user-seed.json")!;
        using var reader = new StreamReader(stream);
        Server = new OfflineServer(Path.Combine(Paths.BepInExRootPath, "offline-data"), reader.ReadToEnd(), resourceRoot);
        PatchRegistry.ApplyAll(harmony);
        Log.LogInfo("Embedded server ready. SDK unchanged; no HTTP transport.");
    }

    public override bool Unload()
    {
        harmony.UnpatchSelf();
        return true;
    }
}
