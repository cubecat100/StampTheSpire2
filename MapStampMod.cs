#nullable enable
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace MapStamp;

[ModInitializer("ModInit")]
public static class MapStampMod
{
    private static Harmony? _harmony;

    public static void ModInit()
    {
        _harmony ??= new Harmony("mapstamp.mod");
        _harmony.PatchAll();
        Log.Warn("[MapStamp] ModInit");
    }
}
