namespace TouhouLastWorld;

public sealed record RecyclePicturesCommand(
    global::System.Int64[]? Ids = null,
    global::TouhouLastWorld.LocalPictureRecycle? PictureRecycle = null);
