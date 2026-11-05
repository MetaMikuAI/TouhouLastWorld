namespace TouhouLastWorld;

public sealed record PrepareFirstDrawCommand(
    global::TouhouLastWorld.LocalIntroDefinitions? IntroDefinitions = null,
    global::System.String? Text = null);
