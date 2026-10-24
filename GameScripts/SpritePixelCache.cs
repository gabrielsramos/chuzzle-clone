using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChuzzleClone.GameScripts;

/// <summary>Populate on the game thread; workers only read the cached CPU pixels.</summary>
internal static class SpritePixelCache
{
    internal sealed record Pixels(int Width, int Height, Color[] Data);

    private static readonly ConditionalWeakTable<Texture2D, Pixels> Cache = new();

    internal static Pixels Get(Texture2D texture) => Cache.GetValue(texture, source =>
    {
        var data = new Color[source.Width * source.Height];
        source.GetData(data);
        return new Pixels(source.Width, source.Height, data);
    });
}
