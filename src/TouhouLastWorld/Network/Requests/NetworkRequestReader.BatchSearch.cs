using static TouhouLastWorld.Patches.ActivePanels;
namespace TouhouLastWorld.Patches;

public partial class NetworkRequestReader
{
    private static RequestDraft? ReadBatchSearch(BatchSearchRequest? request)
    {
        var master = MasterDB.This();
        var setting = master?.Setting;
        var units = User.This()?.unit?.unit_list?.ToArray();
        var items = master?.Item?.ToList()?.ToArray();
        if (request?.quest_id_list == null || master?.CharacterQuest == null || master.CharacterQuestChapter == null
            || master.Event == null || setting == null || units == null || items == null) return null;
        var extra = master.Event.GetCharacterQuestAddCount();
        var quests = new List<LocalBatchQuest>();
        foreach (var id in request.quest_id_list)
        {
            var row = master.CharacterQuest.Find(id);
            var chapter = row == null ? null : master.CharacterQuestChapter.Find(row.character_quest_chapter_id);
            if (row == null || chapter == null || (long)row.count + extra is <= 0 or > int.MaxValue) return null;
            quests.Add(new(id, chapter.target_unit_id, row.count + extra,
                units.Any(unit => unit.unit_id == chapter.target_unit_id && unit.can_join_reincarnation_quest)));
        }
        var rewards = new List<LocalInventoryReward> { new(3, 0, 0, setting.GOLD_MAX), new(6, 0, 0, setting.CRYSTAL_MAX) };
        rewards.AddRange(items.Select(row => new LocalInventoryReward(12, row.id, 0, setting.ITEM_MAX)));
        return new(Batch: new(quests.ToArray(), rewards.ToArray()));
    }
}
