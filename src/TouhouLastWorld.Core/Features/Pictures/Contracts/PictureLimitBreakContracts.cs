using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalPictureLimitBreak(int PictureId, int Current, int Maximum, int[] GoldCosts);
