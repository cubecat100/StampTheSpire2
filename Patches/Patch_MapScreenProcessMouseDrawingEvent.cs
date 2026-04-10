#nullable enable
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace MapStamp;

[HarmonyPatch(typeof(NMapScreen), "ProcessMouseDrawingEvent")]
public static class Patch_MapScreenProcessMouseDrawingEvent
{
    public static bool Prefix(NMapScreen __instance, InputEvent inputEvent)
    {
        if (inputEvent is not InputEventMouseButton mouseButton)
        {
            return true;
        }

        if (mouseButton.Pressed == false || mouseButton.ButtonIndex != MouseButton.Right)
        {
            return true;
        }

        var system = __instance.GetNodeOrNull<MapStampSystem>(MapStampSystem.NodeName);
        if (system?.IsStampModeActive != true)
        {
            return true;
        }

        if (mouseButton.ButtonIndex != MouseButton.Right)
        {
            return true;
        }

        var overlay = __instance.GetNodeOrNull<MapStampOverlay>(MapStampOverlay.NodeName);
        if (overlay == null)
        {
            Log.Warn("[MapStamp] MapStampOverlay not found during right-click intercept");
            return true;
        }

        overlay.ShowPieMenu(mouseButton.Position);
        Log.Warn($"[MapStamp] Opened stamp pie menu from ProcessMouseDrawingEvent: screen={mouseButton.Position}");
        return false;
    }
}
