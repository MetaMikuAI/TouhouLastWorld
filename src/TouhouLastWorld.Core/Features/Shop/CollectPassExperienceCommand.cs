namespace TouhouLastWorld;

public sealed record CollectPassExperienceCommand(
    global::TouhouLastWorld.LocalPassTask? PassTask = null,
    global::System.Int32? Value = null);
