using ChuzzleClone.GameScripts.Scenes;
using mgcore.Inspection;

namespace ChuzzleClone;

[DebugWindow("Scenes")]
public sealed class SceneDebugWindow : IDebugWindow
{
    public void Build(DebugWindowBuilder ui, DebugContext context)
    {
        var game = context.GetGame<Game1>();
        ui.Label("title", "Switch scenes", fontSize: 20, color: "#F4C95D");
        ui.Row("scenes", row =>
        {
            row.Button("gameplay", "Gameplay", () => game.SwitchScene(SceneKind.Gameplay));
            row.Button("animation", "Animation", () => game.SwitchScene(SceneKind.Animation));
        });
        ui.Label("state", "Each scene keeps its state while inactive.", fontSize: 12);
    }
}
