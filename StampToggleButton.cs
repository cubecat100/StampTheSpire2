#nullable enable
using Godot;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace MapStamp;

public partial class StampToggleButton : Button
{
    public const string NodeName = "CustomStampButton";

    private readonly NMapScreen _mapScreen;
    private bool _isStampModeActive;
    private TextureRect _icon = null!;
    private Label _scaleLabel = null!;
    private Tween? _tween;

    private static readonly Color ActiveColor = new(1.0f, 1.0f, 1.0f, 1.0f);
    private static readonly Color InactiveColor = new(0.72f, 0.72f, 0.72f, 1.0f);

    public StampToggleButton(NMapScreen mapScreen)
    {
        _mapScreen = mapScreen;
        Name = NodeName;
        Text = string.Empty;
        TooltipText = "Stamp";
        Flat = true;
        ToggleMode = false;
        FocusMode = FocusModeEnum.All;
        CustomMinimumSize = new Vector2(60.0f, 60.0f);
    }

    public override void _Ready()
    {
        var emptyStyle = new StyleBoxEmpty();
        AddThemeStyleboxOverride("normal", emptyStyle);
        AddThemeStyleboxOverride("hover", emptyStyle);
        AddThemeStyleboxOverride("pressed", emptyStyle);
        AddThemeStyleboxOverride("focus", emptyStyle);
        AddThemeStyleboxOverride("disabled", emptyStyle);

        _icon = new TextureRect();
        _icon.Name = "Icon";
        _icon.Texture = MapStampIcons.ToolbarStamp;
        _icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        _icon.Size = new Vector2(42.0f, 42.0f);
        _icon.Position = new Vector2(9.0f, 9.0f);
        _icon.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_icon);

        _scaleLabel = new Label();
        _scaleLabel.Name = "ScaleLabel";
        _scaleLabel.Text = "x1";
        _scaleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _scaleLabel.VerticalAlignment = VerticalAlignment.Center;
        _scaleLabel.Position = new Vector2(-4.0f, 62.0f);
        _scaleLabel.Size = new Vector2(68.0f, 18.0f);
        _scaleLabel.MouseFilter = MouseFilterEnum.Ignore;
        _scaleLabel.AddThemeFontSizeOverride("font_size", 12);
        _scaleLabel.ZIndex = 10;
        AddChild(_scaleLabel);

        Pressed += OnPressed;
        MouseEntered += OnMouseEntered;
        MouseExited += OnMouseExited;
        FocusEntered += OnFocusEntered;
        FocusExited += OnFocusExited;
        SyncFromSystem();
    }

    private void OnPressed()
    {
        var system = _mapScreen.GetNodeOrNull<MapStampSystem>(MapStampSystem.NodeName);
        if (system == null)
        {
            Log.Warn("[MapStamp] MapStampSystem not found");
            return;
        }

        system.HandleStampButtonPressed();
        SyncState(system.IsStampModeActive, system.CurrentStampScaleLabel);
    }

    public void SetStampModeActive(bool isActive)
    {
        var system = _mapScreen.GetNodeOrNull<MapStampSystem>(MapStampSystem.NodeName);
        var scaleLabel = system?.CurrentStampScaleLabel ?? _scaleLabel.Text;
        SyncState(isActive, scaleLabel);
    }

    public void SyncState(bool isActive, string scaleLabel)
    {
        _isStampModeActive = isActive;
        _scaleLabel.Text = scaleLabel;
        UpdateVisuals();
        QueueRedraw();
    }

    private void SyncFromSystem()
    {
        var system = _mapScreen.GetNodeOrNull<MapStampSystem>(MapStampSystem.NodeName);
        if (system == null)
        {
            UpdateVisuals();
            return;
        }

        _isStampModeActive = system.IsStampModeActive;
        _scaleLabel.Text = system.CurrentStampScaleLabel;
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        TooltipText = _isStampModeActive == true ? $"Stamp: ON {_scaleLabel.Text}" : $"Stamp {_scaleLabel.Text}";
        var color = _isStampModeActive == true ? ActiveColor : InactiveColor;
        _icon.Modulate = color;
        _scaleLabel.Modulate = color;
    }

    private void OnMouseEntered()
    {
        AnimateIcon(isHighlighted: true);
    }

    private void OnMouseExited()
    {
        if (HasFocus() == false)
        {
            AnimateIcon(isHighlighted: false);
        }
    }

    private void OnFocusEntered()
    {
        AnimateIcon(isHighlighted: true);
    }

    private void OnFocusExited()
    {
        AnimateIcon(isHighlighted: false);
    }

    private void AnimateIcon(bool isHighlighted)
    {
        _tween?.Kill();
        _tween = CreateTween().SetParallel();
        _tween.TweenProperty(_icon, "scale", isHighlighted ? Vector2.One * 1.12f : Vector2.One, 0.06);
        _tween.TweenProperty(_icon, "modulate", isHighlighted ? ActiveColor : (_isStampModeActive == true ? ActiveColor : InactiveColor), 0.06);
        _tween.TweenProperty(_scaleLabel, "modulate", isHighlighted ? ActiveColor : (_isStampModeActive == true ? ActiveColor : InactiveColor), 0.06);
    }
}
