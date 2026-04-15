#nullable enable
using Godot;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace MapStamp;

public partial class MapStampOverlay : Control
{
    public const string NodeName = "MapStampOverlay";

    private const float BrowserMargin = 16.0f;
    private const float BrowserMaxWidth = 760.0f;
    private const float BrowserMaxHeight = 460.0f;
    private const float BrowserMinWidth = 320.0f;
    private const float BrowserMinHeight = 240.0f;
    private const float StampEntryWidth = 112.0f;

    private readonly Dictionary<string, Vector2[][]> _strokeCache = [];
    private readonly List<StampTypeDefinition> _stampTypes = [];
    private Control _menuRoot = null!;
    private GridContainer _stampGrid = null!;
    private Label _titleLabel = null!;
    private PanelContainer _browserPanel = null!;
    private NMapScreen _mapScreen = null!;
    private Vector2 _pendingStampPosition;

    public override void _Ready()
    {
        Name = NodeName;
        MouseFilter = MouseFilterEnum.Ignore;
        ProcessMode = ProcessModeEnum.Always;
        SetAnchorsPreset(LayoutPreset.FullRect);
        _mapScreen = GetParent<NMapScreen>();

        BuildStampBrowser();
        RebuildStampEntries();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_menuRoot.Visible == false)
        {
            return;
        }

        if (@event is InputEventKey keyEvent && keyEvent.Pressed == true && keyEvent.Keycode == Key.Escape)
        {
            HidePieMenu();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event is not InputEventMouseButton mouseButton || mouseButton.Pressed == false)
        {
            return;
        }

        if (_browserPanel.GetGlobalRect().HasPoint(mouseButton.Position) == false)
        {
            HidePieMenu();
            GetViewport().SetInputAsHandled();
        }
    }

    public void ShowPieMenu(Vector2 screenPosition)
    {
        _pendingStampPosition = screenPosition;
        RebuildStampEntries();
        UpdateBrowserLayout(screenPosition);
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

    public void ClearStrokeCache()
    {
        _strokeCache.Clear();
    }

    private void BuildStampBrowser()
    {
        _menuRoot = new Control();
        _menuRoot.Name = "StampBrowserRoot";
        _menuRoot.Visible = false;
        _menuRoot.MouseFilter = MouseFilterEnum.Ignore;
        _menuRoot.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_menuRoot);

        _browserPanel = new PanelContainer();
        _browserPanel.Name = "StampBrowserPanel";
        _browserPanel.MouseFilter = MouseFilterEnum.Stop;
        _browserPanel.Size = new Vector2(BrowserMaxWidth, BrowserMaxHeight);
        _menuRoot.AddChild(_browserPanel);

        var margins = new MarginContainer();
        margins.AddThemeConstantOverride("margin_left", 14);
        margins.AddThemeConstantOverride("margin_top", 14);
        margins.AddThemeConstantOverride("margin_right", 14);
        margins.AddThemeConstantOverride("margin_bottom", 14);
        _browserPanel.AddChild(margins);

        var layout = new VBoxContainer();
        layout.Name = "Layout";
        layout.AddThemeConstantOverride("separation", 10);
        margins.AddChild(layout);

        _titleLabel = new Label();
        _titleLabel.Name = "Title";
        _titleLabel.Text = "Stamps";
        _titleLabel.AddThemeFontSizeOverride("font_size", 18);
        layout.AddChild(_titleLabel);

        var scroll = new ScrollContainer();
        scroll.Name = "Scroll";
        scroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
        layout.AddChild(scroll);

        _stampGrid = new GridContainer();
        _stampGrid.Name = "StampGrid";
        _stampGrid.Columns = 4;
        _stampGrid.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _stampGrid.AddThemeConstantOverride("h_separation", 10);
        _stampGrid.AddThemeConstantOverride("v_separation", 10);
        scroll.AddChild(_stampGrid);
    }

    private void HidePieMenu()
    {
        _menuRoot.Visible = false;
    }

    private void RebuildStampEntries()
    {
        LoadStampTypes();
        ClearStampGrid();

        if (_stampTypes.Count == 0)
        {
            _titleLabel.Text = "Stamps (0)";

            var emptyLabel = new Label();
            emptyLabel.Name = "EmptyState";
            emptyLabel.Text = "No stamp images found in stamp_img";
            emptyLabel.HorizontalAlignment = HorizontalAlignment.Center;
            emptyLabel.CustomMinimumSize = new Vector2(240.0f, 64.0f);
            _stampGrid.AddChild(emptyLabel);
            return;
        }

        _titleLabel.Text = $"Stamps ({_stampTypes.Count})";

        foreach (var stampType in _stampTypes)
        {
            _stampGrid.AddChild(CreateMenuButton(stampType));
        }
    }

    private void ClearStampGrid()
    {
        foreach (Node child in _stampGrid.GetChildren())
        {
            _stampGrid.RemoveChild(child);
            child.QueueFree();
        }
    }

    private Button CreateMenuButton(StampTypeDefinition stampType)
    {
        var button = new Button();
        button.Name = $"StampType_{stampType.Id}";
        button.Text = string.Empty;
        button.TooltipText = stampType.Label;
        button.CustomMinimumSize = new Vector2(StampEntryWidth, 104.0f);
        button.FocusMode = FocusModeEnum.None;
        button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        button.Pressed += () => OnStampTypePressed(stampType);

        var content = new CenterContainer();
        content.Name = "Content";
        content.SetAnchorsPreset(LayoutPreset.FullRect);
        content.MouseFilter = MouseFilterEnum.Ignore;
        button.AddChild(content);

        var icon = new TextureRect();
        icon.Name = "Icon";
        icon.Texture = MapStampIcons.GetMenuIcon(stampType.ImageFileName);
        icon.CustomMinimumSize = new Vector2(88.0f, 88.0f);
        icon.ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional;
        icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        icon.MouseFilter = MouseFilterEnum.Ignore;
        content.AddChild(icon);

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

    private void LoadStampTypes()
    {
        _stampTypes.Clear();

        var imageFiles = MapStampIcons.GetAvailableStampImageFiles();
        foreach (var imageFileName in imageFiles)
        {
            var id = Path.GetFileNameWithoutExtension(imageFileName);
            _stampTypes.Add(new StampTypeDefinition(id, id, imageFileName, Vector2.Zero));
        }

        Log.Warn($"[MapStamp] Loaded stamp images: count={_stampTypes.Count}");
    }

    private void UpdateBrowserLayout(Vector2 screenPosition)
    {
        var viewportSize = GetViewportRect().Size;
        var browserWidth = Mathf.Clamp(viewportSize.X - (BrowserMargin * 2.0f), BrowserMinWidth, BrowserMaxWidth);
        var browserHeight = Mathf.Clamp(viewportSize.Y - (BrowserMargin * 2.0f), BrowserMinHeight, BrowserMaxHeight);
        _browserPanel.Size = new Vector2(browserWidth, browserHeight);

        var columns = Mathf.Max(2, Mathf.FloorToInt((browserWidth - 40.0f) / StampEntryWidth));
        _stampGrid.Columns = columns;

        var minX = BrowserMargin;
        var minY = BrowserMargin;
        var maxX = Mathf.Max(BrowserMargin, viewportSize.X - browserWidth - BrowserMargin);
        var maxY = Mathf.Max(BrowserMargin, viewportSize.Y - browserHeight - BrowserMargin);
        var targetX = Mathf.Clamp(screenPosition.X - (browserWidth * 0.5f), minX, maxX);
        var targetY = Mathf.Clamp(screenPosition.Y - (browserHeight * 0.5f), minY, maxY);
        _browserPanel.Position = new Vector2(targetX, targetY);
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
        try
        {
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
        }
        finally
        {
            drawings.SetDrawingModeLocal(DrawingMode.None);
            _mapScreen.Call("UpdateDrawingButtonStates");
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
