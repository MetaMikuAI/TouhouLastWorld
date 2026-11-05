using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalReply(int Status, JsonObject? Body, string Error = "");
public sealed record LocalResourcePaths(string Resource, string Master, string AssetBundle, int AppVersion, int ResourceVersion);
public sealed record LocalProfileUpdate(string? PlayerName = null, string? HeroName = null, string? Comment = null);
public sealed record LocalReplayMeasure(int QuestType, int TargetId, int Difficulty, int? WinRate = null, int? ActionNum = null);
public sealed record LocalReplayContent(int TargetId, int Level, string? Rules, string? Content);
public sealed record LocalTeamUpdate(int Number, string? Name, long[]? Units, int Cost, long[]? EnhancedUnits, int CostWithFriend, int NormalSlotLimit = 0);
public sealed record LocalHomeEntry(int UnitId, long UserUnitId, int CostumeId, int Motion, int Music);
public sealed record LocalHomeUpdate(LocalHomeEntry[]? Units, int SelectedIndex, int RandomMode);
public sealed record LocalEquipmentEntry(int Slot, long PictureId);
public sealed record LocalCostumeEntry(long UserUnitId, int CostumeId, int UnitId, bool IsDefault);
public sealed record LocalRhythmResult(int MusicId, int Difficulty, int Score, float Rate, int Rank);
public sealed record LocalPagePurchase(int Id, int Count, int MaxPages, int UnitCost);
public sealed record LocalSeriesTeam(int Number, long[]? Units);
public sealed record LocalFoodPurchase(int ItemId, int Count, int PointCost, int OrbCost);
public sealed record LocalFoodDistribution(int ItemId, int FoodId, int Count, int Value, int Capacity, int EffectMinutes);
public sealed record LocalTrainingStart(long UnitId, int Skill, int ItemId, int Slot, int Capacity, int Seconds);
public sealed record LocalTrainingMaterial(int ItemId, int Count, int Seconds);
public sealed record LocalPointReset(long UnitId, int GoldCost, bool FreeReset);
public sealed record LocalPointTarget(long Type, long Step);
public sealed record LocalPointRule(int Type, int Step, int Cost, int RequiredUsed);
public sealed record LocalPointAllocation(long UnitId, LocalPointTarget[]? Targets, LocalPointRule[]? Rules);
