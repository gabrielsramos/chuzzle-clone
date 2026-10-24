using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using mgcore.Rendering;

namespace ChuzzleClone.GameScripts.Scenes;

/// <summary>A preview workspace; playback is independent of gameplay's idle animation.</summary>
public sealed class AnimationScene : IGameScene
{
    private readonly Ball _ball;
    private double _elapsed;
    private double _frameDuration = 1.6;
    private bool _previewBallAnimation;

    public RenderEntity Root { get; } = new() { DebugName = "Animation preview" };
    public bool IsPlaying { get; set; } = true;
    public int CurrentFrame { get; private set; }
    public int FrameCount { get; }
    public double FrameDuration => _frameDuration;

    public string ExportSettings() => System.Text.Json.JsonSerializer.Serialize(new
    {
        _ball.ExplosionSpeed,
        _ball.ExplosionAcceleration,
        _ball.ExplosionDuration,
        SpritePreviewFrameDuration = _frameDuration
    }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

    public void AdjustExplosionSpeed(float delta)
    {
        _ball.ExplosionSpeed = Math.Max(0f, _ball.ExplosionSpeed + delta);
        LogExplosionSettings();
    }

    public void AdjustExplosionAcceleration(float delta)
    {
        _ball.ExplosionAcceleration = Math.Max(0f, _ball.ExplosionAcceleration + delta);
        LogExplosionSettings();
    }

    public void AdjustExplosionDuration(float delta)
    {
        _ball.ExplosionDuration = Math.Max(0.1f, MathF.Round(_ball.ExplosionDuration + delta, 2));
        LogExplosionSettings();
    }

    public void RestoreExplosionDefaults()
    {
        _ball.ExplosionSpeed = 170f;
        _ball.ExplosionAcceleration = 900f;
        _ball.ExplosionDuration = 0.4f;
        LogExplosionSettings();
    }

    private void LogExplosionSettings() => Console.WriteLine(
        $"[Animation preview] Explosion: speed {_ball.ExplosionSpeed:0.##} px/s (±33%), " +
        $"acceleration {_ball.ExplosionAcceleration:0.##} px/s², duration {_ball.ExplosionDuration:0.##} s. Applies to the next explosion.");

    public AnimationScene(Texture2D spriteSheet, Vector2 center)
    {
        FrameCount = spriteSheet.Width / Ball.Width;
        _ball = new Ball(spriteSheet, 0, 0);
        _ball.Entity.DebugName = "Preview ball";
        _ball.Entity.Color = Color.White;
        Root.Position = center;
        Root.AddChild(_ball.Entity);
        Reset();
    }

    public void Update(GameTime gameTime)
    {
        if (!IsPlaying) return;
        if (_previewBallAnimation)
        {
            _ball.Update(gameTime);
            return;
        }

        _elapsed += gameTime.ElapsedGameTime.TotalSeconds;
        if (_elapsed < _frameDuration) return;
        var frames = Math.Floor(_elapsed / _frameDuration);
        _elapsed %= _frameDuration;
        SetFrame(CurrentFrame + (int)(frames % FrameCount));
    }

    public void StepFrame(int direction)
    {
        UseSpriteSheetPreview();
        IsPlaying = false;
        _elapsed = 0;
        SetFrame(CurrentFrame + direction);
    }

    public void ChangeSpeed(double multiplier)
    {
        _frameDuration = Math.Clamp(_frameDuration / multiplier, 0.025, 2.0);
        _elapsed = 0;
        Console.WriteLine($"[Animation preview] Frame duration: {_frameDuration:0.###} seconds");
    }

    public void Reset()
    {
        UseSpriteSheetPreview();
        _elapsed = 0;
        SetFrame(0);
    }

    public void StartAnimation(BallAnimations animation)
    {
        _ball.StartAnimation(animation, () =>
            Console.WriteLine($"[Animation preview] {animation} completed"));
        _previewBallAnimation = true;
        _elapsed = 0;
        IsPlaying = true;
    }

    public void StopAnimation()
    {
        _ball.StopAnimation();
        IsPlaying = false;
        _elapsed = 0;
    }

    private void UseSpriteSheetPreview()
    {
        // Starting Idle restores the sprite and removes any explosion fragments.
        _ball.StartAnimation(BallAnimations.Idle);
        _ball.StopAnimation();
        _previewBallAnimation = false;
    }

    private void SetFrame(int frame)
    {
        CurrentFrame = (frame % FrameCount + FrameCount) % FrameCount;
        _ball.Entity.SourceRectangle = new Rectangle(
            CurrentFrame * Ball.Width, 0, Ball.Width, Ball.Height);
        _ball.Entity.Tags["Frame"] = CurrentFrame.ToString();
    }

    public void Dispose() { }
}
