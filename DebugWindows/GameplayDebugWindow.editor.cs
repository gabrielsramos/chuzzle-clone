using mgcore.Inspection;

namespace ChuzzleClone;

[DebugWindow("Gameplay")]
public sealed class GameplayDebugWindow : IDebugWindow
{
    private bool _verbose;

    public void Build(DebugWindowBuilder ui, DebugContext context)
    {
        var game = context.GetGame<Game1>();
        ui.Label("title", "Gameplay tools", fontSize: 20, color: "#F4C95D");
        ui.Label("game", $"Running: {game.Window.Title}", fontSize: 12, color: "#9AA4B2");
        ui.Space("title-gap", 12);
        ui.Separator("options");
        ui.Checkbox("verbose", "Verbose logging", _verbose, enabled =>
        {
            _verbose = enabled;
            Console.WriteLine($"[Gameplay debug] Verbose logging: {enabled}");
        }, fontSize: 14);
        ui.Space("actions-gap", 8);
        ui.Row("actions", row =>
        {
            row.Button("hello", "Hello", () =>
                Console.WriteLine("[Gameplay debug] Button callback executed."));
            row.Button("status", "Status", () =>
                Console.WriteLine($"[Gameplay debug] Verbose logging: {_verbose}"), color: "#F4C95D");
        }, gap: 8);
        ui.Space("grid-gap", 8);
        ui.Separator("grid-divider");
        ui.Label("grid-title", "Grid animations", fontSize: 16, color: "#F4C95D");
        ui.Button("explode-all", "Explode all balls", () => game.ExplodeAllBalls());
        ui.Row("explode-lines", row =>
        {
            row.Button("explode-middle-row", "Explode middle row", () => game.ExplodeMiddleRow());
            row.Button("explode-middle-column", "Explode middle column", () => game.ExplodeMiddleColumn());
        });
        ui.Label("middle-help", "Middle targets the 4th row or column of the 6×6 grid.", fontSize: 12);
        ui.Space("reset-gap", 8);
        ui.Separator("reset-divider");
        ui.Button("reset-grid", "Reset grid", () => game.ResetGrid());
    }
}
