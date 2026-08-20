using ChuzzleClone.Utils;
using mgcore.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace ChuzzleClone.GameScripts;



public class Grid
{
    private GraphicsDevice _graphicsDevice;
    
    private Texture2D _circleTexture;
    
    private RenderEntity _root;
    private Ball[,] _balls;

    private MouseState _previousMouseState;
    private bool _isMousePressed;
    private bool _pressStartedOnBall;
    private Coord _pressedBallCoordinates;
    
    private const int rows = 6;
    private const int columns = 6;
    private const int _spacing = 5;
    
    public int Width
    {
        get
        {
            var diameter = Ball.Width;

            return rows * diameter + (rows + 1) * _spacing;
        }
    }

    public int Height {
        get
        {
            var diameter = Ball.Width;

            return columns * diameter + (columns + 1) * _spacing;
        }
    }

    public Grid(GraphicsDevice graphicsDevice, Texture2D circleCircleTexture)
    {
        _graphicsDevice = graphicsDevice;
        _circleTexture = circleCircleTexture;
        _root = new RenderEntity();
        _balls = new Ball[rows, columns];
    }

    public void Initialize(Vector2 rootPosition)
    {
        var rect = DrawerHelper.CreateRectangleOutline(_graphicsDevice, Width, Height, 2, Color.Black);
        _root = new RenderEntity()
        {
            Texture = rect,
            Position = rootPosition
        };
        
        var topLeft = new Vector2(-Width / 2.0f, -Height / 2.0f);
        var circleRadius = Ball.Width / 2;
        
        //TODO this is flipped, it's initializing rows in place of columns
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < columns; j++)
            {
                var x = topLeft.X + _spacing + circleRadius + (i * (circleRadius * 2 + _spacing));
                var y = topLeft.Y + _spacing + circleRadius + (j * (circleRadius * 2 + _spacing));

                var ball = new Ball(_circleTexture);
                ball.Position = new Vector2(x, y);
                ball.SetCoords(i, j);
                _balls[i, j] = ball;
                
                _root.AddChild(_balls[i, j].Entity);
            }
        }
    }

    public void Update(GameTime gameTime)
    {
        UpdateBalls(gameTime);
        
        var mouseState = Mouse.GetState();

        if (mouseState.LeftButton == ButtonState.Pressed)
        {
            if (!_isMousePressed)
            {
                (_pressStartedOnBall, _pressedBallCoordinates) = IsInsideBall(mouseState.Position.ToVector2());
            }
            else if (_pressStartedOnBall)
            {
                var delta = mouseState.Position.ToVector2() - _previousMouseState.Position.ToVector2();

                if (delta.LengthSquared() > 0)
                {
                    string direction;

                    if (Math.Abs(delta.X) > Math.Abs(delta.Y))
                    {
                        direction = delta.X > 0 ? "right" : "left";
                        MoveBallsHoriontally(_pressedBallCoordinates, delta.X);

                    }
                    else
                    {
                        direction = delta.Y > 0 ? "down" : "up";
                    }

                    Console.WriteLine($"Mouse moving {direction}, diff: X={delta.X:0.##}, Y={delta.Y:0.##}");
                }
            }

            _isMousePressed = true;
        }
        else
        {
            _isMousePressed = false;
        }

        _previousMouseState = mouseState;
    }

    private void UpdateBalls(GameTime gameTime)
    {
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < columns; j++)
            {
                _balls[i, j].Update(gameTime);
            }
        }
    }

    private void MoveBallsHoriontally(Coord coordinates, float delta)
    {
        //TODO go over all balls of that row and update their coordinates accordingly
        var row = coordinates.row;

        for (int j = 0; j < columns; j++)
        {
            var ent = _balls[row, j].Entity;
            var position = ent.Position;
            position.X += delta;
            ent.Position = position;
        }
    }

    private (bool, Coord) IsInsideBall(Vector2 point)
    {
        var radius = Ball.Width / 2f;
        var radiusSquared = radius * radius;
        var coord = new Coord();

        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < columns; j++)
            {
                var ballCenter = _root.Position + _balls[i, j].Position;

                if ((point - ballCenter).LengthSquared() <= radiusSquared)
                {
                    coord.row = i;
                    coord.column = j;
                    return (true, coord);
                }
            }
        }

        return (false, coord);
    }
    

    public RenderEntity GetGrid()
    {
        return _root;
    }
}