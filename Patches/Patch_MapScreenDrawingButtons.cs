#nullable enable
using HarmonyLib;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace MapStamp;

[HarmonyPatch(typeof(NMapScreen), "OnMapDrawingButtonPressed")]
public static class Patch_MapScreenOnMapDrawingButtonPressed
{
    public static void Prefix(NMapScreen __instance)
    {
        __instance.GetNodeOrNull<MapStampSystem>(MapStampSystem.NodeName)?.DeactivateStampMode();
    }
}

[HarmonyPatch(typeof(NMapScreen), "OnMapErasingButtonPressed")]
public static class Patch_MapScreenOnMapErasingButtonPressed
{
    public static void Prefix(NMapScreen __instance)
    {
        __instance.GetNodeOrNull<MapStampSystem>(MapStampSystem.NodeName)?.DeactivateStampMode();
    }
}

[HarmonyPatch(typeof(NMapDrawButton), "SetIsDrawing")]
public static class Patch_MapDrawButtonSetIsDrawing
{
    public static void Postfix(NMapDrawButton __instance, bool isDrawing)
    {
        if (isDrawing == false)
        {
            return;
        }

        MapScreenPatchHelpers.FindMapScreen(__instance)?.GetNodeOrNull<MapStampSystem>(MapStampSystem.NodeName)?.DeactivateStampMode();
    }
}

[HarmonyPatch(typeof(NMapEraseButton), "SetIsErasing")]
public static class Patch_MapEraseButtonSetIsErasing
{
    public static void Postfix(NMapEraseButton __instance, bool isErasing)
    {
        if (isErasing == false)
        {
            return;
        }

        MapScreenPatchHelpers.FindMapScreen(__instance)?.GetNodeOrNull<MapStampSystem>(MapStampSystem.NodeName)?.DeactivateStampMode();
    }
}

internal static class MapScreenPatchHelpers
{
    internal static NMapScreen? FindMapScreen(Node node)
    {
        Node? current = node;
        while (current != null)
        {
            if (current is NMapScreen mapScreen)
            {
                return mapScreen;
            }

            current = current.GetParent();
        }

        return null;
    }
}
