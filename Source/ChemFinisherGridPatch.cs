using HarmonyLib;

/// <summary>
/// Runs after the vanilla OpenUI has built its vertical list. Replaces that list
/// with the grid when the option is enabled; otherwise restores the vanilla
/// layout so toggling the option back off works without a restart.
/// </summary>
[HarmonyPatch(typeof(ChemFinisherUIManager), nameof(ChemFinisherUIManager.OpenUI))]
internal static class ChemFinisherGridPatch
{
    [HarmonyPostfix]
    private static void Postfix(ChemFinisherUIManager __instance)
    {
        ChemFinisherGridUi.OnOpened(__instance);
    }
}
