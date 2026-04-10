#nullable enable
using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace MapStamp;

[HarmonyPatch(typeof(NMapScreen), "_Ready")]
public static class Patch_MapScreenReady
{
    public static void Postfix(NMapScreen __instance)
    {
        var drawingTools = __instance.GetNodeOrNull<NinePatchRect>("%DrawingTools");
        var clearButton = drawingTools?.GetNodeOrNull<NMapClearButton>("%ClearButton");
        var drawingToolRow = drawingTools?.GetChildOrNull<HBoxContainer>(0);

        if (drawingTools == null || clearButton == null || drawingToolRow == null)
        {
            Log.Warn("[MapStamp] Drawing toolbar nodes not found");
            return;
        }

        if (drawingTools.GetNodeOrNull<StampToggleButton>(StampToggleButton.NodeName) == null)
        {
            var stampButton = new StampToggleButton(__instance);
            clearButton.AddSibling(stampButton);
            clearButton.FocusNeighborRight = $"../{StampToggleButton.NodeName}";
            stampButton.FocusNeighborLeft = "../ClearButton";
            Log.Warn("[MapStamp] Custom stamp button added to drawing toolbar");
        }

        var tree = drawingTools.GetTree();
        if (tree != null)
        {
            Action callback = null!;
            callback = () =>
            {
                tree.ProcessFrame -= callback;
                ResizeDrawingToolsForChildren(drawingTools, drawingToolRow);
            };
            tree.ProcessFrame += callback;
        }

        if (__instance.GetNodeOrNull<MapStampSystem>(MapStampSystem.NodeName) == null)
        {
            __instance.AddChild(new MapStampSystem());
            Log.Warn("[MapStamp] MapStampSystem attached");
        }

        if (__instance.GetNodeOrNull<MapStampOverlay>(MapStampOverlay.NodeName) == null)
        {
            __instance.AddChild(new MapStampOverlay());
            Log.Warn("[MapStamp] MapStampOverlay attached");
        }
    }

    public static void ResizeDrawingToolsForChildren(NinePatchRect drawingTools, HBoxContainer drawingToolRow)
    {
        var requiredWidth = drawingToolRow.GetCombinedMinimumSize().X + 32.0f;
        if (requiredWidth > drawingTools.Size.X)
        {
            drawingTools.Size = new Vector2(requiredWidth, drawingTools.Size.Y);
        }
    }
}
