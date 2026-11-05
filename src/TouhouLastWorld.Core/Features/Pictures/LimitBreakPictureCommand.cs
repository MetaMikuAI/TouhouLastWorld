namespace TouhouLastWorld;

public sealed record LimitBreakPictureCommand(
    global::System.Int64? EntityId = null,
    global::System.Int64[]? Ids = null,
    global::TouhouLastWorld.LocalPictureLimitBreak? PictureLimitBreak = null);
