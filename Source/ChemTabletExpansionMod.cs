using System;
using System.Collections.Generic;

/// <summary>
/// Adds the two tablet recipes, the gridded recipe UI and the two mod options.
/// No item definitions, bottled recipes or save fields are added.
/// </summary>
public sealed class ChemTabletExpansionMod : IMod
{
    public const string ModId = "chem_tablet_expansion";
    public const string ModVersion = "1.1.3";

    public const int TabletVolume = 15000; // 15 mL, in the machine's microlitre unit

    // Legacy ids from 1.3.x; only used to clear a saved bottle-mode selection.
    private const string LegacyNutrientBottleId = "full_nutrient_tablet_box";
    private const string LegacyWaterBottleId = "full_water_purification_tablet_box";

    private static readonly string[] RecipeIds =
    {
        "nutrient_tablet",
        "water_tablet"
    };

    private static readonly string[] DisplayNameKeys =
    {
        "item_hydroponic_nutrient_tablet_name",
        "item_water_tablet_name"
    };

    private static readonly string[] ActiveIngredients =
    {
        "mineral",
        "chlorine"
    };

    private static readonly List<ChemFinisherRecipe> Installed = new List<ChemFinisherRecipe>();

    private static ConfigEntry<bool> _redrawUi;
    private static ConfigEntry<int> _columnsPerRow;
    private static bool _configInitialized;

    private bool _hooked;

    internal static bool RedrawUi => _redrawUi == null || _redrawUi.Value;
    internal static int ColumnsPerRow
    {
        get
        {
            int value = _columnsPerRow == null ? 6 : _columnsPerRow.Value;
            return Math.Max(4, Math.Min(8, value));
        }
    }


    public void Init(ModManifest manifest)
    {
        InitConfig();
        Hook();
    }

    public void OnEnable()
    {
        Hook();
    }

    public void OnDisable()
    {
        Unhook();
        Uninstall();
    }

    private static void InitConfig()
    {
        if (_configInitialized)
        {
            return;
        }
        _configInitialized = true;

        ModConfig config = ModHelper.GetConfig(ModId);

        _redrawUi = config.Define(
            "redraw_ui",
            true,
            "UI",
            () => "重绘配方界面 / Redraw recipe UI",
            () => "把化学制剂机的配方列表改成网格布局。 / Show the Chemical Finisher recipes as a grid.");

        _columnsPerRow = config.Define(
            "columns_per_row",
            6,
            "UI",
            () => "每行物品数量 / Items per row",
            tooltip: () => "网格每行的物品数量，输入 4-8，默认 6；超出范围时按 4 或 8 生效。 / Items per row, enter 4-8, default 6; out-of-range values are clamped to 4 or 8.");
    }

    private void Hook()
    {
        if (_hooked)
        {
            return;
        }

        ModHook.OnGameLoadedInit += Install;
        ModHook.OnGameLoadedLate += OnGameLoadedLate;
        ModHook.OnNewGameStarted += OnNewGameStarted;
        ModHook.OnModConfigApplied += OnConfigApplied;
        _hooked = true;
    }

    private void Unhook()
    {
        if (!_hooked)
        {
            return;
        }

        ModHook.OnGameLoadedInit -= Install;
        ModHook.OnGameLoadedLate -= OnGameLoadedLate;
        ModHook.OnNewGameStarted -= OnNewGameStarted;
        ModHook.OnModConfigApplied -= OnConfigApplied;
        _hooked = false;
    }

    private static void OnNewGameStarted(string scenarioId)
    {
        Install();
    }

    private static void OnGameLoadedLate()
    {
        ClearLegacyBottleSelections();
    }

    private static void OnConfigApplied(string modId)
    {
        if (!string.Equals(modId, ModId, StringComparison.Ordinal) || _columnsPerRow == null)
        {
            return;
        }

        int value = _columnsPerRow.Value;
        int clamped = Math.Max(4, Math.Min(8, value));
        if (clamped != value)
        {
            _columnsPerRow.Set(clamped);
        }
    }

    private static void Install()
    {
        List<ChemFinisherRecipe> recipes = ChemFinisherRecipe.chemFinisherRecipes;
        if (recipes == null)
        {
            return;
        }

        for (int i = 0; i < RecipeIds.Length; i++)
        {
            string id = RecipeIds[i];
            if (IsInstalled(id) || ChemFinisherRecipe.GetRecipeById(id) != null)
            {
                continue;
            }

            ChemFinisherRecipe recipe = new ChemFinisherRecipe
            {
                identifier = id,
                displayName = GetDisplayName(DisplayNameKeys[i]),
                requiredVolume = TabletVolume,
                isRequireDry = true,
                activeIngredientId = ActiveIngredients[i]
            };

            recipes.Add(recipe);
            Installed.Add(recipe);
        }
    }

    private static void ClearLegacyBottleSelections()
    {
        ChemFinisherUIManager ui = ChemFinisherUIManager.Instance;
        if (ui != null && ui.currentItem != null)
        {
            ClearLegacyBottleSelection(ui.currentItem);
        }

        EmporiumEntry emporium = EmporiumEntry.Instance;
        List<GameItem> items = emporium != null ? emporium.GetAllItems() : null;
        if (items == null)
        {
            return;
        }

        for (int i = 0; i < items.Count; i++)
        {
            ClearLegacyBottleSelection(items[i]);
        }
    }

    private static void ClearLegacyBottleSelection(GameItem item)
    {
        if (item == null || !item.IsTag("SELECTED_CHEM_ID_TAG"))
        {
            return;
        }

        string selected = item.GetTagReadonly("SELECTED_CHEM_ID_TAG").GetString();
        if (string.Equals(selected, LegacyNutrientBottleId, StringComparison.Ordinal) ||
            string.Equals(selected, LegacyWaterBottleId, StringComparison.Ordinal))
        {
            item.DisableTag("SELECTED_CHEM_ID_TAG");
        }
    }

    private static bool IsInstalled(string id)
    {
        for (int i = 0; i < Installed.Count; i++)
        {
            ChemFinisherRecipe recipe = Installed[i];
            if (recipe == null || !string.Equals(recipe.identifier, id, StringComparison.Ordinal))
            {
                continue;
            }

            List<ChemFinisherRecipe> live = ChemFinisherRecipe.chemFinisherRecipes;
            if (live != null && live.Contains(recipe))
            {
                return true;
            }

            Installed.RemoveAt(i);
            return false;
        }

        return false;
    }

    private static void Uninstall()
    {
        if (Installed.Count == 0)
        {
            return;
        }

        List<ChemFinisherRecipe> recipes = ChemFinisherRecipe.chemFinisherRecipes;
        if (recipes != null)
        {
            for (int i = Installed.Count - 1; i >= 0; i--)
            {
                ChemFinisherRecipe recipe = Installed[i];
                if (recipe != null)
                {
                    recipes.Remove(recipe);
                }
            }
        }

        Installed.Clear();
    }

    private static string GetDisplayName(string key)
    {
        try
        {
            string name = LocHelper.GetLocalizedItem(key);
            if (!string.IsNullOrEmpty(name))
            {
                return name;
            }
        }
        catch (Exception)
        {
            // Fall through to the key. The vanilla item table always has these
            // two keys, so this only happens on a broken install.
        }

        return key;
    }
}
