using mgcore.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Diagnostics;

namespace ChuzzleClone.GameScripts;

public struct Coord
{
    public int row;
    public int column;
}

public enum BallAnimations { Idle, Exploding }

public enum BallColor { Green, Blue, Purple, Red, Yellow }

public enum ExplosionPhase
{
    CreateParticles,
    Disperse,
    Disappear
}

public class Ball : IDisposable
{
    public BallColor BallColor { get; }

    public BallAnimations? CurrentAnimation { get; private set; }
    public ExplosionPhase? CurrentExplosionPhase { get; private set; }

    private float _explosionSpeed = 170f;
    private float _explosionAcceleration = 900f;
    private float _explosionDuration = 0.4f;

    public float ExplosionSpeed
    {
        get => _explosionSpeed;
        set => _explosionSpeed = ValidateSetting(value, 0f, nameof(ExplosionSpeed));
    }

    public float ExplosionAcceleration
    {
        get => _explosionAcceleration;
        set => _explosionAcceleration = ValidateSetting(value, 0f, nameof(ExplosionAcceleration));
    }

    public float ExplosionDuration
    {
        get => _explosionDuration;
        set => _explosionDuration = ValidateSetting(value, 0.1f, nameof(ExplosionDuration));
    }

    public RenderEntity Entity
    {
        get
        {
            return _entity;
        }
    }

    public Vector2 Position
    {
        get { return _entity.Position; }
        set { _entity.Position = value; }
    }

    public Coord GridCoordinates;
    
    public static int Width => _frameWidth;
    public static int Height => _frameHeight;


    private RenderEntity _entity;
    private readonly Texture2D _spriteSheet;
    private readonly SpritePixelCache.Pixels _spritePixels;

    private sealed record ParticleSnapshot(
        Texture2D Texture, SpritePixelCache.Pixels Pixels, Rectangle Frame,
        Vector2 Origin, Color Color, SpriteEffects Effects, float LayerDepth,
        float Speed, float Acceleration, float Duration, int Seed);

    private sealed record PreparedParticles(List<Particle> Particles, double PreparationMilliseconds);
    private Task<PreparedParticles>? _pendingParticles;
    private CancellationTokenSource? _particlePreparationCancellation;
    internal double ParticlePreparationMilliseconds { get; private set; }
    internal double ParticleAttachmentMilliseconds { get; private set; }
    private sealed class Particle
    {
        public required RenderEntity Entity { get; init; }
        public Vector2 Velocity;
        public Vector2 Acceleration;
        public Color InitialColor;
        public float Lifetime;
    }

    private readonly List<Particle> _particles = new();
    private float _disperseElapsed;
    private float _activeExplosionSpeed;
    private float _activeExplosionAcceleration;
    private float _activeExplosionDuration;
    private float _timer;
    private int _currentFrame = 0;
    private int _previousFrame = 0;
    private Random _random = new Random();
    
    private float _frameTime;
    private Action? _onAnimationCompleted;
    
    private const int _frameWidth = 100;
    private const int _frameHeight = 100;
    private const int _framesAmount = 6;
    private const float _maxFrameTime = 20.0f;
    private const int _particleSize = 10;

    public Ball(Texture2D spriteSheet, BallColor color, int row, int column)
    {
        BallColor = color;
        _spriteSheet = spriteSheet;
        _spritePixels = SpritePixelCache.Get(spriteSheet);
        _entity = new RenderEntity();
        _entity.Texture = spriteSheet;
        _entity.Color = Color.White;
        _currentFrame = _random.Next(0, _framesAmount);
        _previousFrame = _currentFrame;
        Rectangle? sourceRectangle = new Rectangle(
            _currentFrame * _frameWidth,
            0,
            _frameWidth,
            _frameHeight);
        _entity.SourceRectangle = sourceRectangle;
        _frameTime = GetRandomFrameTime();
        _entity.DebugName = $"ball {row}x{column}";
        SetCoords(row, column);
        StartAnimation(BallAnimations.Idle);
    }

    public void SetCoords(int row, int column)
    {
        GridCoordinates.row = row;
        GridCoordinates.column = column;
    }
    
    public void Update(GameTime gameTime)
    {
        switch (CurrentAnimation)
        {
            case BallAnimations.Idle:
                UpdateIdle(gameTime);
                break;
            case BallAnimations.Exploding:
                UpdateExploding(gameTime);
                break;
        }
    }

    /// <summary>Restarts playback, cancelling any previous completion callback.</summary>
    public void StartAnimation(BallAnimations animation, Action? onCompleted = null)
    {
        if (!Enum.IsDefined(animation))
            throw new ArgumentOutOfRangeException(nameof(animation));

        StopAnimation();
        RestoreSprite();
        CurrentAnimation = animation;
        _onAnimationCompleted = onCompleted;

        if (animation == BallAnimations.Exploding)
        {
            _activeExplosionSpeed = ExplosionSpeed;
            _activeExplosionAcceleration = ExplosionAcceleration;
            _activeExplosionDuration = ExplosionDuration;
            CurrentExplosionPhase = ExplosionPhase.CreateParticles;
            BeginParticlePreparation();
        }
    }

    /// <summary>Freezes the current frame and cancels the completion callback.</summary>
    public void StopAnimation()
    {
        _particlePreparationCancellation?.Cancel();
        _particlePreparationCancellation?.Dispose();
        _particlePreparationCancellation = null;
        // A discarded worker owns only detached particles and cannot publish its result.
        _pendingParticles = null;
        CurrentAnimation = null;
        CurrentExplosionPhase = null;
        _onAnimationCompleted = null;
        _timer = 0;
        _disperseElapsed = 0;
    }

    public void Dispose()
    {
        StopAnimation();
        RemoveParticles();
        _entity.Parent?.RemoveChild(_entity);
        _entity.Texture = null;
    }

    private void UpdateExploding(GameTime gameTime)
    {
        switch (CurrentExplosionPhase)
        {
            case ExplosionPhase.CreateParticles:
                TryAttachPreparedParticles();
                break;
            case ExplosionPhase.Disperse:
                UpdateDisperse(gameTime);
                break;
            case ExplosionPhase.Disappear:
                RemoveParticles();
                CompleteAnimation();
                break;
        }
    }

    private void BeginParticlePreparation()
    {
        var frame = _entity.SourceRectangle
            ?? new Rectangle(0, 0, _spritePixels.Width, _spritePixels.Height);
        if (frame.Width <= 0 || frame.Height <= 0 || frame.X < 0 || frame.Y < 0
            || frame.Right > _spritePixels.Width || frame.Bottom > _spritePixels.Height)
            throw new InvalidOperationException("The ball frame must be inside the cached sprite sheet.");
        var origin = _entity.Origin ?? new Vector2(frame.Width / 2f, frame.Height / 2f);
        var snapshot = new ParticleSnapshot(_spriteSheet, _spritePixels, frame, origin,
            _entity.Color, _entity.Effects, _entity.LayerDepth,
            _activeExplosionSpeed, _activeExplosionAcceleration, _activeExplosionDuration, _random.Next());
        ParticlePreparationMilliseconds = 0;
        ParticleAttachmentMilliseconds = 0;
        _particlePreparationCancellation = new CancellationTokenSource();
        var token = _particlePreparationCancellation.Token;
        _pendingParticles = Task.Run(() => BuildParticles(snapshot, token), token);
        // Observe faults even if Stop/restart discards this preparation task.
        _ = _pendingParticles.ContinueWith(task =>
            Console.Error.WriteLine($"[Particles] Preparation failed: {task.Exception!.GetBaseException().Message}"),
            CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
    }

    private static PreparedParticles BuildParticles(ParticleSnapshot snapshot, CancellationToken token)
    {
        var started = Stopwatch.GetTimestamp();
        var frame = snapshot.Frame;
        var random = new Random(snapshot.Seed);
        var particles = new List<Particle>();

        for (var y = 0; y < frame.Height; y += _particleSize)
        {
            for (var x = 0; x < frame.Width; x += _particleSize)
            {
                token.ThrowIfCancellationRequested();
                var width = Math.Min(_particleSize, frame.Width - x);
                var height = Math.Min(_particleSize, frame.Height - y);
                var visible = false;
                for (var py = y; py < y + height && !visible; py++)
                    for (var px = x; px < x + width; px++)
                        if (snapshot.Pixels.Data[(frame.Y + py) * snapshot.Pixels.Width + frame.X + px].A != 0)
                        {
                            visible = true;
                            break;
                        }
                if (!visible) continue;

                var offsetX = snapshot.Effects.HasFlag(SpriteEffects.FlipHorizontally)
                    ? frame.Width - x - width : x;
                var offsetY = snapshot.Effects.HasFlag(SpriteEffects.FlipVertically)
                    ? frame.Height - y - height : y;
                var particle = new RenderEntity
                {
                    DebugName = $"Ball fragment {x},{y}",
                    Texture = snapshot.Texture,
                    SourceRectangle = new Rectangle(frame.X + x, frame.Y + y, width, height),
                    Origin = Vector2.Zero,
                    Position = new Vector2(offsetX, offsetY) - snapshot.Origin,
                    Color = snapshot.Color,
                    Effects = snapshot.Effects,
                    LayerDepth = snapshot.LayerDepth
                };
                var fromCenter = new Vector2(
                    offsetX + width / 2f - frame.Width / 2f,
                    offsetY + height / 2f - frame.Height / 2f);
                var angle = fromCenter.LengthSquared() > 0
                    ? MathF.Atan2(fromCenter.Y, fromCenter.X)
                    : random.NextSingle() * MathF.Tau;
                // Keep each trajectory mostly radial, with up to ten degrees of variation.
                angle += (random.NextSingle() * 2f - 1f) * MathF.PI / 18f;
                var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                particles.Add(new Particle
                {
                    Entity = particle,
                    Velocity = direction * snapshot.Speed
                        * (2f / 3f + random.NextSingle() * 2f / 3f),
                    Acceleration = direction * snapshot.Acceleration,
                    InitialColor = particle.Color,
                    Lifetime = snapshot.Duration * (0.6f + random.NextSingle() * 0.4f)
                });
            }
        }

        return new PreparedParticles(particles, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
    }

    private void TryAttachPreparedParticles()
    {
        var task = _pendingParticles;
        if (task is null || !task.IsCompleted) return;
        if (!task.IsCompletedSuccessfully)
        {
            StopAnimation();
            return;
        }

        // The task is already finished; retrieving its result cannot block the game loop.
        var prepared = task.GetAwaiter().GetResult();
        var started = Stopwatch.GetTimestamp();
        _particles.AddRange(prepared.Particles);
        foreach (var particle in _particles)
            _entity.AddChild(particle.Entity);
        _entity.Texture = null;
        _pendingParticles = null;
        _particlePreparationCancellation?.Dispose();
        _particlePreparationCancellation = null;
        ParticlePreparationMilliseconds = prepared.PreparationMilliseconds;
        ParticleAttachmentMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        CurrentExplosionPhase = ExplosionPhase.Disperse;
    }

    private void UpdateDisperse(GameTime gameTime)
    {
        // Clamp the final step so a long frame cannot overshoot the dispersal duration.
        var dt = Math.Clamp((float)gameTime.ElapsedGameTime.TotalSeconds,
            0f, _activeExplosionDuration - _disperseElapsed);
        var nextElapsed = _disperseElapsed + dt;
        for (var i = _particles.Count - 1; i >= 0; i--)
        {
            var particle = _particles[i];
            var particleDt = Math.Min(dt, Math.Max(0f, particle.Lifetime - _disperseElapsed));
            particle.Entity.Position += particle.Velocity * particleDt
                + 0.5f * particle.Acceleration * particleDt * particleDt;
            particle.Velocity += particle.Acceleration * particleDt;
            if (nextElapsed >= particle.Lifetime)
            {
                _entity.RemoveChild(particle.Entity);
                _particles.RemoveAt(i);
                continue;
            }

            var opacity = Math.Clamp(
                (particle.Lifetime - nextElapsed) / (particle.Lifetime * 0.2f), 0f, 1f);
            // Scale all channels to preserve premultiplied-alpha blending.
            particle.Entity.Color = particle.InitialColor * opacity;
        }

        _disperseElapsed = nextElapsed;
        if (_particles.Count == 0)
        {
            CurrentExplosionPhase = ExplosionPhase.Disappear;
            RemoveParticles();
            CompleteAnimation();
        }
    }

    private void RestoreSprite()
    {
        RemoveParticles();
        _entity.Texture = _spriteSheet;
    }

    private void RemoveParticles()
    {
        foreach (var particle in _particles)
            _entity.RemoveChild(particle.Entity);
        _particles.Clear();
    }

    private static float ValidateSetting(float value, float minimum, string name)
    {
        if (!float.IsFinite(value) || value < minimum)
            throw new ArgumentOutOfRangeException(name, $"Expected a finite value of at least {minimum}.");
        return value;
    }

    private void CompleteAnimation()
    {
        var onCompleted = _onAnimationCompleted;
        StopAnimation();
        onCompleted?.Invoke();
    }

    private void UpdateIdle(GameTime gameTime)
    {
        _timer += (float)gameTime.ElapsedGameTime.TotalSeconds;
        
        if (_timer >= _frameTime)
        {
            _timer = 0;
            _currentFrame = (_currentFrame + 1) % _framesAmount;
        }

        if (_previousFrame != _currentFrame)
        {
            Rectangle? sourceRectangle = new Rectangle(
                _currentFrame * _frameWidth,
                0,
                _frameWidth,
                _frameHeight);
            _entity.SourceRectangle = sourceRectangle;
            _previousFrame = _currentFrame;
            _frameTime = GetRandomFrameTime();
        }
        
    }

    private float GetRandomFrameTime()
    {
        return (float)_random.NextDouble() * _maxFrameTime + 0.5f;
    }
}
