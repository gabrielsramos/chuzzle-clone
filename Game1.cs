using ChuzzleClone.GameScripts;
using ChuzzleClone.GameScripts.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using mgcore.Rendering;

namespace ChuzzleClone;

public class Game1 : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private Renderer _renderer = null!;
    private readonly RenderScene _scene = new();
    private GameplayScene _gameplayScene = null!;
    public AnimationScene AnimationScene { get; private set; } = null!;
    public SceneKind CurrentScene { get; private set; } = SceneKind.Gameplay;
    private IGameScene ActiveScene => CurrentScene == SceneKind.Gameplay
        ? _gameplayScene : AnimationScene;

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
        _renderer = new Renderer(GraphicsDevice);
        
        (BallColor Color, Texture2D Texture)[] ballTextures =
        [
            (BallColor.Green, Content.Load<Texture2D>("balls/green")),
            (BallColor.Blue, Content.Load<Texture2D>("balls/blue")),
            (BallColor.Purple, Content.Load<Texture2D>("balls/purple")),
            (BallColor.Red, Content.Load<Texture2D>("balls/red")),
            (BallColor.Yellow, Content.Load<Texture2D>("balls/yellow"))
        ];
        // Read back each sheet once during loading, before any explosions can be requested.
        foreach (var sprite in ballTextures)
            SpritePixelCache.Get(sprite.Texture);
        
        var center = new Vector2(
            GraphicsDevice.Viewport.Width / 2f,
            GraphicsDevice.Viewport.Height / 2f);
        
        _gameplayScene = new GameplayScene(GraphicsDevice, ballTextures, center);
        AnimationScene = new AnimationScene(ballTextures[4].Texture, center);
        _scene.AddWorld(ActiveScene.Root);
    }

    public void ExplodeAllBalls()
    {
        SwitchScene(SceneKind.Gameplay);
        _gameplayScene.ExplodeAllBalls();
    }

    public void ExplodeMiddleRow()
    {
        SwitchScene(SceneKind.Gameplay);
        _gameplayScene.ExplodeMiddleRow();
    }

    public void ExplodeMiddleColumn()
    {
        SwitchScene(SceneKind.Gameplay);
        _gameplayScene.ExplodeMiddleColumn();
    }

    public void ResetGrid()
    {
        SwitchScene(SceneKind.Gameplay);
        _gameplayScene.ResetGrid();
    }

    public void SwitchScene(SceneKind scene)
    {
        if (!Enum.IsDefined(scene)) throw new ArgumentOutOfRangeException(nameof(scene));
        if (CurrentScene == scene) return;
        CurrentScene = scene;
        // Keep one inspection host and publish only the active scene's entities.
        _scene.Clear();
        _scene.AddWorld(ActiveScene.Root);
        Console.WriteLine($"[Scenes] Active scene: {scene}");
    }

    protected override void Update(GameTime gameTime)
    {
        ActiveScene.Update(gameTime);
        
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.DimGray);
        
        _renderer.DrawScene(_scene);

        base.Draw(gameTime);
    }
    
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _scene.Dispose();
            _gameplayScene?.Dispose();
            AnimationScene?.Dispose();
        }
        base.Dispose(disposing);
    }
}
