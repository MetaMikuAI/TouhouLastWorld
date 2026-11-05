using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalIntroDefinitions(int[] UnitIds, int[] PictureIds);
public sealed record LocalIntroPicture(int Id, int Parameter1, int Parameter2, int Characteristic);
public sealed record LocalIntroGrant(int UnitCapacity, int PictureCapacity, LocalIntroPicture[] Pictures);
