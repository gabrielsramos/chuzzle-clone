using mgcore.Inspection;
using ChuzzleClone.GameScripts;
using System.Runtime.InteropServices;

namespace ChuzzleClone;

[DebugWindow("Animation")]
public sealed class AnimationDebugWindow : IDebugWindow
{
    public void Build(DebugWindowBuilder ui, DebugContext context)
    {
        var game = context.GetGame<Game1>();
        // Resolve preview state in callbacks; the interface caches this window schema.
        ui.Label("title", "Ball animation preview", fontSize: 20, color: "#F4C95D");
        ui.Label("help", "Open the Animation scene using the Scenes window.", fontSize: 12);
        Section(ui, "animation", "Animation");
        ui.Row("animations", row =>
        {
            row.Button("idle", "Idle", () => game.AnimationScene.StartAnimation(BallAnimations.Idle));
            row.Button("explode", "Explode", () => game.AnimationScene.StartAnimation(BallAnimations.Exploding));
            row.Button("stop", "Stop", () => game.AnimationScene.StopAnimation());
        });
        Section(ui, "playback", "Playback");
        ui.Row("playback", row =>
        {
            row.Button("play", "Play", () => game.AnimationScene.IsPlaying = true);
            row.Button("pause", "Pause", () => game.AnimationScene.IsPlaying = false);
        });
        Section(ui, "explosion", "Explosion settings");
        ui.Label("explosion-settings-help", "Changes apply on the next run. Current values are logged to the console.", fontSize: 12);
        ui.Label("explosion-speed-label", "Initial speed (px/s, ±33% variation)", fontSize: 12);
        ui.Row("explosion-speed", row =>
        {
            row.Button("explosion-speed-down", "−10", () => game.AnimationScene.AdjustExplosionSpeed(-10f));
            row.Button("explosion-speed-up", "+10", () => game.AnimationScene.AdjustExplosionSpeed(10f));
        });
        ui.Label("explosion-acceleration-label", "Acceleration (px/s²)", fontSize: 12);
        ui.Row("explosion-acceleration", row =>
        {
            row.Button("explosion-acceleration-down", "−100", () => game.AnimationScene.AdjustExplosionAcceleration(-100f));
            row.Button("explosion-acceleration-up", "+100", () => game.AnimationScene.AdjustExplosionAcceleration(100f));
        });
        ui.Label("explosion-duration-label", "Maximum duration (seconds, minimum 0.1)", fontSize: 12);
        ui.Row("explosion-duration", row =>
        {
            row.Button("explosion-duration-down", "−0.1", () => game.AnimationScene.AdjustExplosionDuration(-0.1f));
            row.Button("explosion-duration-up", "+0.1", () => game.AnimationScene.AdjustExplosionDuration(0.1f));
        });
        Section(ui, "sprite-preview", "Sprite-sheet preview");
        ui.Button("reset", "Reset to first frame", () => game.AnimationScene.Reset());
        ui.Row("frames", row =>
        {
            row.Button("previous", "Previous frame", () => game.AnimationScene.StepFrame(-1));
            row.Button("next", "Next frame", () => game.AnimationScene.StepFrame(1));
        });
        ui.Label("step-help", "Stepping and Reset return to sprite-sheet preview. Stepping pauses; Reset keeps the playback state.", fontSize: 12);
        ui.Row("speed", row =>
        {
            row.Button("slower", "Half speed", () => game.AnimationScene.ChangeSpeed(0.5));
            row.Button("faster", "Double speed", () => game.AnimationScene.ChangeSpeed(2));
        });
        ui.Label("speed-help", "Sprite-sheet preview only: starts at 1.6 seconds/frame. Range: 0.5–40 frames/sec.", fontSize: 12);
        Section(ui, "settings-actions", "Settings actions");
        ui.Row("settings-actions", row =>
        {
            row.Button("copy-settings", "Copy settings", () => CopySettings(game.AnimationScene.ExportSettings()));
            row.Button("explosion-defaults", "Restore defaults", () => game.AnimationScene.RestoreExplosionDefaults());
        });
        ui.Label("defaults-help", "Restore defaults resets explosion settings only. Copy includes sprite preview speed.", fontSize: 12);
    }

    private static void Section(DebugWindowBuilder ui, string id, string title)
    {
        ui.Space(id + "-space", 8);
        ui.Separator(id + "-divider");
        ui.Label(id + "-heading", title, fontSize: 16, color: "#F4C95D");
    }

    private static void CopySettings(string text)
    {
        try
        {
            if (SDL_SetClipboardText(text) != 0)
            {
                Console.WriteLine("[Animation preview] Could not copy settings: " +
                    Marshal.PtrToStringUTF8(SDL_GetError()));
                Console.WriteLine(text);
                return;
            }
            Console.WriteLine("[Animation preview] Settings copied to clipboard.");
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            Console.WriteLine($"[Animation preview] Clipboard unavailable: {exception.Message}");
            Console.WriteLine(text);
        }
    }

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    private static extern int SDL_SetClipboardText([MarshalAs(UnmanagedType.LPUTF8Str)] string text);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr SDL_GetError();
}
