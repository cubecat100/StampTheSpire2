#nullable enable
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using System.Reflection;

namespace MapStamp;

public partial class MapStampSystem : Node
{
    public const string NodeName = "MapStampSystem";
    public const Key TestPieMenuKey = Key.F7;
    public const Key TestDrawingKey = Key.F8;
    public const int MinStampScaleStep = 1;
    public const int MaxStampScaleStep = 3;
    private static readonly FieldInfo? DrawingInputField = AccessTools.Field(typeof(NMapScreen), "_drawingInput");

    public bool IsStampModeActive { get; set; }
    public int CurrentStampScaleStep { get; private set; } = MinStampScaleStep;
    public float CurrentStampScaleMultiplier => CurrentStampScaleStep;
    public string CurrentStampScaleLabel => $"x{CurrentStampScaleStep}";

    public MapStampSystem()
    {
        Name = NodeName;
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _Ready()
    {
        SetProcessUnhandledInput(true);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent)
        {
            HandlePieMenuTestKey(keyEvent);
            HandleDrawingTestKey(keyEvent);
        }
    }

    private void HandlePieMenuTestKey(InputEventKey keyEvent)
    {
        if (keyEvent.Pressed == false || keyEvent.Echo == true)
        {
            return;
        }

        if (keyEvent.Keycode != TestPieMenuKey)
        {
            return;
        }

        var overlay = GetParent()?.GetNodeOrNull<MapStampOverlay>(MapStampOverlay.NodeName);
        if (overlay == null)
        {
            Log.Warn("[MapStamp] MapStampOverlay not found");
            return;
        }

        var mousePosition = GetViewport().GetMousePosition();
        overlay.ShowPieMenu(mousePosition);
        Log.Warn($"[MapStamp] Opened test pie menu with {TestPieMenuKey}: screen={mousePosition}");
        GetViewport().SetInputAsHandled();
    }

    private void HandleDrawingTestKey(InputEventKey keyEvent)
    {
        if (keyEvent.Pressed == false || keyEvent.Echo == true)
        {
            return;
        }

        if (keyEvent.Keycode != TestDrawingKey)
        {
            return;
        }

        var mapScreen = GetParent<NMapScreen>();
        var overlay = mapScreen.GetNodeOrNull<MapStampOverlay>(MapStampOverlay.NodeName);
        if (overlay == null)
        {
            Log.Warn("[MapStamp] MapStampOverlay not found for F8 test");
            return;
        }

        var mousePosition = GetViewport().GetMousePosition();
        overlay.DrawDebugTestAt(mousePosition);
        Log.Warn($"[MapStamp] Requested direct drawing test with {TestDrawingKey}: screen={mousePosition}");
        GetViewport().SetInputAsHandled();
    }

    public void DeactivateStampMode()
    {
        IsStampModeActive = false;

        var mapScreen = GetParent<NMapScreen>();
        var stampButton = mapScreen.GetNodeOrNull<StampToggleButton>($"%{StampToggleButton.NodeName}")
            ?? mapScreen.GetNodeOrNull<StampToggleButton>(StampToggleButton.NodeName);
        stampButton?.SetStampModeActive(false);

        var overlay = mapScreen.GetNodeOrNull<MapStampOverlay>(MapStampOverlay.NodeName);
        overlay?.ClosePieMenu();

        Log.Warn("[MapStamp] Stamp mode deactivated");
    }

    public void HandleStampButtonPressed()
    {
        if (IsStampModeActive == true)
        {
            CycleStampScaleStep();
            return;
        }

        ActivateStampMode();
    }

    public void ActivateStampMode()
    {
        StopExistingMapDrawingMode();
        IsStampModeActive = true;
        SyncStampButton();
        Log.Warn($"[MapStamp] Stamp mode activated: scale={CurrentStampScaleLabel}");
    }

    public void CycleStampScaleStep()
    {
        CurrentStampScaleStep++;
        if (CurrentStampScaleStep > MaxStampScaleStep)
        {
            CurrentStampScaleStep = MinStampScaleStep;
        }

        var overlay = GetParent<NMapScreen>().GetNodeOrNull<MapStampOverlay>(MapStampOverlay.NodeName);
        overlay?.ClearStrokeCache();
        SyncStampButton();
        Log.Warn($"[MapStamp] Stamp scale changed: scale={CurrentStampScaleLabel}");
    }

    private void SyncStampButton()
    {
        var mapScreen = GetParent<NMapScreen>();
        var stampButton = mapScreen.GetNodeOrNull<StampToggleButton>($"%{StampToggleButton.NodeName}")
            ?? mapScreen.GetNodeOrNull<StampToggleButton>(StampToggleButton.NodeName);
        stampButton?.SyncState(IsStampModeActive, CurrentStampScaleLabel);
    }

    private void StopExistingMapDrawingMode()
    {
        var mapScreen = GetParent<NMapScreen>();
        var drawingInput = DrawingInputField?.GetValue(mapScreen) as NMapDrawingInput;

        mapScreen.Drawings?.SetDrawingModeLocal(DrawingMode.None);

        if (drawingInput != null)
        {
            drawingInput.StopDrawing();
            drawingInput.QueueFree();
            DrawingInputField?.SetValue(mapScreen, null);
        }

        mapScreen.Call("UpdateDrawingButtonStates");
        Log.Warn("[MapStamp] Cleared existing drawing state before activating stamp mode");
    }
}
