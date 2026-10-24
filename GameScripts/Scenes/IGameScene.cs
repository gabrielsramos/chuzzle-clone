using Microsoft.Xna.Framework;
using mgcore.Rendering;

namespace ChuzzleClone.GameScripts.Scenes;

public enum SceneKind { Gameplay, Animation }

public interface IGameScene : IDisposable
{
    RenderEntity Root { get; }
    void Update(GameTime gameTime);
}
