#nullable enable
using Godot;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace MapStamp;

public partial class MapStampOverlay : Control
{
    public const string NodeName = "MapStampOverlay";

    private readonly Dictionary<string, Vector2[][]> _strokeCache = [];
    private readonly List<StampTypeDefinition> _stampTypes = [];
    private Control _menuRoot = null!;
    private NMapScreen _mapScreen = null!;
    private Vector2 _pendingStampPosition;

    public override void _Ready()
    {
        Name = NodeName;
        MouseFilter = MouseFilterEnum.Ignore;
        ProcessMode = ProcessModeEnum.Always;
        SetAnchorsPreset(LayoutPreset.FullRect);
        _mapScreen = GetParent<NMapScreen>();

        _menuRoot = new Control();
        _menuRoot.Name = "StampPieMenu";
        _menuRoot.Visible = false;
        _menuRoot.MouseFilter = MouseFilterEnum.Pass;
        AddChild(_menuRoot);

        LoadStampTypes();

        foreach (var stampType in _stampTypes)
        {
            _menuRoot.AddChild(CreateMenuButton(stampType));
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_menuRoot.Visible == false)
        {
            return;
        }

        if (@event is not InputEventMouseButton mouseButton)
        {
            return;
        }

        if (mouseButton.Pressed == false)
        {
            return;
        }

        if (mouseButton.ButtonIndex == MouseButton.Left || mouseButton.ButtonIndex == MouseButton.Right)
        {
            HidePieMenu();
        }
    }

    public void ShowPieMenu(Vector2 screenPosition)
    {
        _pendingStampPosition = screenPosition;
        _menuRoot.Position = screenPosition;
        _menuRoot.Visible = true;
    }

    public void ClosePieMenu()
    {
        HidePieMenu();
    }

    public void DrawDebugTestAt(Vector2 screenPosition)
    {
        var debugStrokes = new[]
        {
            new[] { new Vector2(-18.0f, -18.0f), new Vector2(18.0f, 18.0f) },
            new[] { new Vector2(18.0f, -18.0f), new Vector2(-18.0f, 18.0f) },
            new[] { new Vector2(-22.0f, 0.0f), new Vector2(22.0f, 0.0f) },
        };

        DrawStrokes("debug", screenPosition, debugStrokes);
    }

    private void HidePieMenu()
    {
        _menuRoot.Visible = false;
    }

    private Button CreateMenuButton(StampTypeDefinition stampType)
    {
        var button = new Button();
        button.Name = $"StampType_{stampType.Id}";
        button.Text = string.Empty;
        button.TooltipText = stampType.Label;
        button.Size = new Vector2(112.0f, 112.0f);
        button.Position = stampType.Offset - (button.Size / 2.0f);
        button.FocusMode = FocusModeEnum.None;
        button.Pressed += () => OnStampTypePressed(stampType);

        var icon = new TextureRect();
        icon.Name = "Icon";
        icon.Texture = MapStampIcons.GetMenuIcon(stampType.ImageFileName);
        icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        icon.Size = new Vector2(80.0f, 80.0f);
        icon.Position = new Vector2(16.0f, 16.0f);
        icon.MouseFilter = MouseFilterEnum.Ignore;
        button.AddChild(icon);

        return button;
    }

    private void OnStampTypePressed(StampTypeDefinition stampType)
    {
        DrawStamp(stampType, _pendingStampPosition);
        HidePieMenu();
        Log.Warn($"[MapStamp] Selected stamp type: id={stampType.Id} label={stampType.Label}");
    }

    private void DrawStamp(StampTypeDefinition stampType, Vector2 screenPosition)
    {
        var system = _mapScreen.GetNodeOrNull<MapStampSystem>(MapStampSystem.NodeName);
        var scaleMultiplier = system?.CurrentStampScaleMultiplier ?? 1.0f;
        var cacheKey = CreateStrokeCacheKey(stampType.ImageFileName, scaleMultiplier);

        if (_strokeCache.TryGetValue(cacheKey, out var strokes) == false)
        {
            var sourceImage = MapStampIcons.GetStampSourceImage(stampType.ImageFileName);
            strokes = MapStampImageStrokeGenerator.Generate(sourceImage, scaleMultiplier);
            _strokeCache[cacheKey] = strokes;
        }

        DrawStrokes(stampType.Id, screenPosition, strokes);
    }

    public void ClearStrokeCache()
    {
        _strokeCache.Clear();
    }

    private void LoadStampTypes()
    {
        _stampTypes.Clear();

        var imageFiles = MapStampIcons.GetAvailableStampImageFiles();
        for (int i = 0; i < imageFiles.Count; i++)
        {
            var imageFileName = imageFiles[i];
            var id = Path.GetFileNameWithoutExtension(imageFileName);
            _stampTypes.Add(new StampTypeDefinition(id, id, imageFileName, GetMenuOffset(i, imageFiles.Count)));
        }

        Log.Warn($"[MapStamp] Loaded stamp images: count={_stampTypes.Count}");
    }

    private static Vector2 GetMenuOffset(int index, int count)
    {
        if (count <= 1)
        {
            return Vector2.Zero;
        }

        if (count == 2)
        {
            return index == 0 ? new Vector2(-72.0f, 0.0f) : new Vector2(72.0f, 0.0f);
        }

        const float halfPi = 1.5707964f;
        const float tau = 6.2831855f;
        var radius = count <= 5 ? 108.0f : 144.0f;
        var angle = (-halfPi) + ((tau / count) * index);
        return new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
    }

    private void DrawStrokes(string stampId, Vector2 screenPosition, Vector2[][] strokes)
    {
        var drawings = _mapScreen.Drawings;
        if (drawings == null)
        {
            Log.Warn($"[MapStamp] Drawings is null for {stampId}");
            return;
        }

        drawings.SetDrawingModeLocal(DrawingMode.Drawing);

        foreach (var stroke in strokes)
        {
            if (stroke.Length == 0)
            {
                continue;
            }

            var start = ToDrawingPosition(screenPosition + stroke[0]);
            drawings.BeginLineLocal(start, DrawingMode.Drawing);

            for (int i = 1; i < stroke.Length; i++)
            {
                var point = ToDrawingPosition(screenPosition + stroke[i]);
                drawings.UpdateCurrentLinePositionLocal(point);
            }

            drawings.StopLineLocal();
        }

        Log.Warn($"[MapStamp] Drew strokes: id={stampId} strokeCount={strokes.Length} screen={screenPosition}");
    }

    private Vector2 ToDrawingPosition(Vector2 screenPosition)
    {
        var netPosition = _mapScreen.GetNetPositionFromScreenPosition(screenPosition);
        var mapPosition = _mapScreen.Call("GetMapPositionFromNetPosition", netPosition);
        return mapPosition.AsVector2();
    }

    private static string CreateStrokeCacheKey(string imageFileName, float scaleMultiplier)
    {
        return $"{imageFileName}@{scaleMultiplier.ToString("0.###", CultureInfo.InvariantCulture)}";
    }
}
