using System.Drawing;

namespace WinInspector.Core.Models;

public readonly record struct WindowsRect(int X, int Y, int Width, int Height)
{
    public static WindowsRect Empty => new(0, 0, 0, 0);

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public int Right => X + Width;
    public int Bottom => Y + Height;

    public Rectangle ToRectangle() => new(X, Y, Math.Max(Width, 0), Math.Max(Height, 0));

    public static WindowsRect FromRectangle(Rectangle rectangle) =>
        rectangle.IsEmpty
            ? Empty
            : new(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
}
