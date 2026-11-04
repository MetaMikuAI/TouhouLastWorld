using System;

using BepInEx.Logging;

namespace TouhouLastWorld;

[AttributeUsage(AttributeTargets.Class)]
public sealed class AutoLogAttribute : Attribute;

public sealed class LogWrapper(ManualLogSource source, string tag)
{
    public void Info(string message) => source.LogInfo($"[{tag}] {message}");
    public void Warning(string message) => source.LogWarning($"[{tag}] {message}");
    public void Error(string message) => source.LogError($"[{tag}] {message}");
}
