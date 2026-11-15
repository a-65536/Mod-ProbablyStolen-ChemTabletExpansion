using System;
using System.Collections.Generic;

/// <summary>
/// The whole mod: append two vanilla-shaped recipes to the chemical finisher.
/// No Harmony patches, no UI, no prompts, no item or save changes.
/// </summary>
public sealed class ChemTabletExpansionMod : IMod
{
    private const int TabletVolume = 15000; // 15 mL, in the machine's microlitre unit

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

    private bool _hooked;

    public void Init(ModManifest manifest)
    {
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

    private void Hook()
    {
        if (_hooked)
        {
            return;
        }

        ModHook.OnGameLoadedInit += Install;
        ModHook.OnNewGameStarted += OnNewGameStarted;
        _hooked = true;
    }

    private void Unhook()
    {
        if (!_hooked)
        {
            return;
        }

        ModHook.OnGameLoadedInit -= Install;
        ModHook.OnNewGameStarted -= OnNewGameStarted;
        _hooked = false;
    }

    private static void OnNewGameStarted(string scenarioId)
    {
        Install();
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
