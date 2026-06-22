namespace JabInspector.Core.Models;

public sealed record ElementBounds(int X, int Y, int Width, int Height);
public sealed record LocatorSuggestion(string Engine, string Role, string Name, string Description, string States,
    int IndexInParent, string Path, ElementBounds Bounds);
