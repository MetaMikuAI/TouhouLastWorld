namespace TouhouLastWorld;

public sealed record ShortenTrainingCommand(
    global::System.Int64? EntityId = null,
    global::TouhouLastWorld.LocalTrainingMaterial[]? TrainingMaterials = null);
