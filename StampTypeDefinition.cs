#nullable enable
using Godot;

namespace MapStamp;

public sealed record StampTypeDefinition(
    string Id,
    string Label,
    string ImageFileName,
    Vector2 Offset);
