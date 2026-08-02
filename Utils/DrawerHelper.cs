using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChuzzleClone.Utils;

public static class DrawerHelper
{
    public static Texture2D CreateRectangleOutline(
        GraphicsDevice graphicsDevice,
        int width,
        int height,
        int thickness,
        Color color)
    {
        Texture2D texture = new Texture2D(graphicsDevice, width, height);
        Color[] pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool isBorder =
                    x < thickness ||              // left edge
                    x >= width - thickness ||     // right edge
                    y < thickness ||              // top edge
                    y >= height - thickness;      // bottom edge

                pixels[y * width + x] = isBorder ? color : Color.Transparent;
            }
        }

        texture.SetData(pixels);
        return texture;
    }
}