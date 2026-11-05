namespace TouhouLastWorld;

public sealed record GameRequest(string Api, IApiInput? Payload = null, int? Count = null, int? GachaId = null, int? GachaButtonId = null, LocalDailyQuestDay? Day = null, RefreshFacilityStockCommand? FacilityIncome = null, LocalProgressMission[]? ProgressMissions = null);
