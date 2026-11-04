using System.Collections;

using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;

using NativeRoutine = Il2CppSystem.Collections.IEnumerator;

namespace TouhouLastWorld.Patches;

[HarmonyPatch(typeof(global::HomeBehaviour))]
public static class HomeBehaviourPatch
{
    [HarmonyPatch(nameof(HomeBehaviour.Awake))]
    [HarmonyPrefix]
    public static void Awake_Prefix(HomeBehaviour __instance)
    {
        // APK 内置旧场景缺少这些可选节点；新客户端初始化仍会访问它们。
        __instance.lostWordLiveButton = ExistingOrEmpty(__instance, __instance.lostWordLiveButton, "UnavailableLiveLink");
        __instance.conditionalPackButton = ExistingOrEmpty(__instance, __instance.conditionalPackButton, "UnavailableConditionalPack");
        __instance.GGbattlePassBadge = ExistingOrEmpty(__instance, __instance.GGbattlePassBadge, "UnavailablePassBadge");
        __instance.GGbattlePassButton = ExistingOrEmpty(__instance, __instance.GGbattlePassButton, "UnavailablePassButton");
    }

    private static void PrepareOptionalComponents(HomeBehaviour __instance)
    {
        if (__instance.DeliveryIcon == null)
            __instance.DeliveryIcon = EmptyIcon(__instance.transform, "UnavailableDeliveryIcon");
        if (__instance.BattlePassIcon == null)
            __instance.BattlePassIcon = EmptyIcon(__instance.transform, "UnavailableBattlePassIcon");
        var menu = __instance.specialMenuCtrl;
        if (menu.m_seriesButton == null)
        {
            var node = Child(menu.transform, "UnavailableSeries");
            var button = node.AddComponent<HomeSpecialButton>();
            button.m_OffGO = Child(node.transform, "Off");
            button.m_OnGO = Child(node.transform, "On");
            button.m_CloseGO = Child(node.transform, "Closed");
            button.m_BadgeGO = Child(node.transform, "Badge");
            menu.m_seriesButton = button;
        }
        if (__instance.achievement == null)
        {
            var node = Child(__instance.transform, "UnavailableAchievement");
            var plate = node.AddComponent<AchievementPlate>();
            plate.m_nameText = Child(node.transform, "Name").AddComponent<UnityEngine.UI.Text>();
            __instance.achievement = plate;
        }
        if (__instance.m_idleSearchButton == null)
        {
            var node = Child(__instance.transform, "UnavailableIdleSearch");
            var idle = node.AddComponent<HomeIdleSearchButton>();
            idle.m_idleSearchButton = Child(node.transform, "Icon").AddComponent<UnityEngine.UI.Image>();
            idle.m_progressBar = Child(node.transform, "Progress").AddComponent<UnityEngine.UI.Image>();
            idle.m_rewardSprites = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<UnityEngine.Sprite>(1L);
            idle.m_completeBadge = Child(node.transform, "Complete");
            idle.m_updateBadge = Child(node.transform, "Update");
            __instance.m_idleSearchButton = idle;
        }
    }

    private static UnityEngine.GameObject Child(UnityEngine.Transform parent, string name)
    {
        var node = new UnityEngine.GameObject(name);
        node.transform.SetParent(parent, false);
        return node;
    }

    private static UnityEngine.CanvasGroup EmptyIcon(UnityEngine.Transform parent, string name)
    {
        var node = Child(parent, name);
        node.AddComponent<UnityEngine.UI.Image>();
        return node.AddComponent<UnityEngine.CanvasGroup>();
    }

    private static UnityEngine.GameObject ExistingOrEmpty(HomeBehaviour home, UnityEngine.GameObject existing, string name)
    {
        if (existing != null) return existing;
        var node = new UnityEngine.GameObject(name);
        node.transform.SetParent(home.transform, false);
        node.SetActive(false);
        return node;
    }

    [HarmonyPatch(nameof(HomeBehaviour.Awake))]
    [HarmonyPostfix]
    public static void Awake_Postfix(HomeBehaviour __instance)
        => Plugin.Instance.Log.LogInfo($"Home Awake completed; enabled={__instance.enabled}");

    [HarmonyPatch(nameof(HomeBehaviour.Start))]
    [HarmonyPostfix]
    public static void Start_Postfix(HomeBehaviour __instance, ref NativeRoutine __result)
        => __result = Observe(__instance, __result).WrapToIl2Cpp();

    private static IEnumerator Observe(HomeBehaviour home, NativeRoutine original)
    {
        PrepareOptionalComponents(home);
        var state = original.TryCast<HomeBehaviour._Start_d__153>();
        var lastState = int.MinValue;
        while (original.MoveNext())
        {
            if (state != null && lastState != state.__1__state)
            {
                lastState = state.__1__state;
                Plugin.Instance.Log.LogInfo($"Home initialization stage: {lastState}");
            }
            yield return original.Current;
        }
        Plugin.Instance.Log.LogInfo("Home initialization completed.");
    }
}
