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

public static class ActivePanels
{
    public static T? FindActivePanel<T>() where T : UnityEngine.Component
    {
        foreach (var candidate in UnityEngine.Resources.FindObjectsOfTypeAll(Il2CppType.Of<T>()))
        {
            var panel = candidate.TryCast<T>();
            if (panel != null && panel.gameObject.activeInHierarchy && panel.gameObject.scene.isLoaded)
                return panel;
        }
        return null;
    }

}
