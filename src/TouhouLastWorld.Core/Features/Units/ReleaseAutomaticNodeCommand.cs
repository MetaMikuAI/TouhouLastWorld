namespace TouhouLastWorld;

public sealed record ReleaseAutomaticNodeCommand(
    global::TouhouLastWorld.LocalAutomaticRelease? AutomaticRelease = null,
    global::System.Int64? EntityId = null,
    global::System.Int32? Value = null);
