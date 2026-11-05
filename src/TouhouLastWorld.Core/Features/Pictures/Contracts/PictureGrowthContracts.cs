using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalPictureGrowth(int PictureId, int Current, int Next, int Maximum, int ItemId, int ItemCount, int GoldCost);
