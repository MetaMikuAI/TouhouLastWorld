using System.IO;

using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

using TouhouLastWorld.Patches;

namespace TouhouLastWorld;

[BepInPlugin(Id, PluginBuildInfo.Name, PluginBuildInfo.Version)]
public sealed class Plugin : BasePlugin
{
    public const string Id = PluginBuildInfo.Id;
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
        var paths = ReadResourcePaths();
        using var stream = typeof(Plugin).Assembly.GetManifestResourceStream("TouhouLastWorld.user-seed.json")!;
        using var reader = new StreamReader(stream);
        Server = new OfflineServer(new JsonStateStore(Path.Combine(Paths.BepInExRootPath, "offline-data")), reader.ReadToEnd(), resourceRoot, paths);
        PatchRegistry.ApplyAll(harmony);
        Log.LogInfo("Embedded server ready. SDK unchanged; no HTTP transport.");
    }

    private LocalResourcePaths? ReadResourcePaths()
    {
        var resource = Config.Bind("Assets", "ResourcePath", "", "version_get 返回的资源目录。").Value.Trim().TrimEnd('/');
        var master = Config.Bind("Assets", "MasterPath", "", "version_get 返回的主表目录。").Value.Trim().TrimEnd('/');
        var bundles = Config.Bind("Assets", "AssetBundlePath", "", "version_get 返回的安卓 AssetBundle 目录。").Value.Trim().TrimEnd('/');
        var appVersion = Config.Bind("Assets", "AppVersion", 92, "version_get 返回的客户端版本。").Value;
        var resourceVersion = Config.Bind("Assets", "ResourceVersion", 5019, "version_get 返回的资源版本。").Value;
        var paths = new[] { resource, master, bundles };
        if (paths.All(string.IsNullOrEmpty)) return null;
        if (appVersion <= 0 || resourceVersion < 0 || !paths.All(path => Uri.TryCreate(path, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment)))
        {
            Log.LogWarning("Assets requires three HTTPS directories and valid versions. Using bundled assets.");
            return null;
        }
        return new(resource, master, bundles, appVersion, resourceVersion);
    }

    public override bool Unload()
    {
        harmony.UnpatchSelf();
        return true;
    }
}
