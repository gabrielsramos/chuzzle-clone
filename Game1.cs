using ChuzzleClone.GameScripts;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using mgcore.Rendering;

namespace ChuzzleClone;

public class Game1 : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private Renderer _renderer = null!;
    private Grid _grid;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        _graphics.PreferredBackBufferWidth = 1280;
        _graphics.PreferredBackBufferHeight = 720;
        _graphics.ApplyChanges();
        
        Window.Title = "Chuzzle Clone";
        
        Content.RootDirectory = "Content";

        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _renderer = new Renderer(new SpriteBatch(GraphicsDevice));
        
        _grid = new Grid(GraphicsDevice, Content.Load<Texture2D>("circle"));
        
        var center = new Vector2(
            GraphicsDevice.Viewport.Width / 2f,
            GraphicsDevice.Viewport.Height / 2f);
        
        _grid.Initialize(center);
    }

    protected override void Update(GameTime gameTime)
    {
        _grid.UpdatePositions();
        
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.DimGray);
        
        var gridObject = _grid.GetGrid();
        
        _renderer.DrawWorld(new[] { gridObject }, Matrix.Identity);

        base.Draw(gameTime);
    }
}
