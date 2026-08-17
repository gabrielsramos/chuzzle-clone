using mgcore.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChuzzleClone.GameScripts;

public class Ball
{
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
    
    public static int Width => _frameWidth;
    public static int Height => _frameHeight;


    private RenderEntity _entity;
    private float _timer;
    private int _currentFrame = 0;
    private int _previousFrame = 0;
    private bool _hasAnimationStarted;
    private Random _random = new Random();
    
    private float _frameTime;
    
    private const int _frameWidth = 100;
    private const int _frameHeight = 100;
    private const int _framesAmount = 6;
    private const float _maxFrameTime = 3.0f;

    public Ball(Texture2D spriteSheet)
    {
        _entity = new RenderEntity();
        _entity.Texture = spriteSheet;
        _currentFrame = _random.Next(0, _framesAmount);
        Rectangle? sourceRectangle = new Rectangle(
            _currentFrame * _frameWidth,
            0,
            _frameWidth,
            _frameHeight);
        _entity.SourceRectangle = sourceRectangle;
        _frameTime = GetRandomFrameTime();
    }
    
    public void Update(GameTime gameTime)
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