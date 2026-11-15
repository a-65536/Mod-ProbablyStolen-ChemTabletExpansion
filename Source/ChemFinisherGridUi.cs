using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

/// <summary>
/// Replaces the vanilla vertical recipe list with a configurable grid of cells.
/// Cell: centred item icon, name, core material + volume, dry requirement, and a
/// thin rectangular selection outline.
/// </summary>
internal static class ChemFinisherGridUi
{
    private const float Padding = 12f;
    private const float Spacing = 12f;
    private const float FallbackWidth = 666.6f;

    private static readonly Dictionary<string, Sprite> ItemIconCache = new Dictionary<string, Sprite>();
    private static readonly Dictionary<GameObject, Vector2> VanillaContentSizes =
        new Dictionary<GameObject, Vector2>();
    private static readonly Dictionary<GameObject, bool> VanillaFitterStates =
        new Dictionary<GameObject, bool>();
    private static readonly Dictionary<string, Outline> SelectionOutlines =
        new Dictionary<string, Outline>();

    private static readonly Color CellBackground = new Color(209f / 255f, 200f / 255f, 165f / 255f, 1f);
    private static readonly Color NameColor = new Color(65f / 255f, 69f / 255f, 84f / 255f, 1f);
    private static readonly Color FallbackMaterial = new Color(0.28f, 0.25f, 0.18f, 1f);
    private static readonly Color FallbackRequirement = new Color(0.55f, 0.18f, 0.08f, 1f);
    private static readonly Color SelectionColor = new Color(0.98f, 0.55f, 0.06f, 1f);

    private sealed class CellStyle
    {
        public TMP_FontAsset Font;
        public Color MaterialColor;
        public Color RequirementColor;
    }

    public static void OnOpened(ChemFinisherUIManager ui)
    {
        if (ui == null || ui.LayoutGroup == null)
        {
            return;
        }

        if (!ChemTabletExpansionMod.RedrawUi)
        {
            RestoreVanillaLayout(ui.LayoutGroup);
            return;
        }

        try
        {
            BuildGrid(ui);
        }
        catch (Exception e)
        {
            Debug.LogError("[ChemTabletExpansion] Grid UI failed: " + e);
            RestoreVanillaLayout(ui.LayoutGroup);
        }
    }

    private static void RestoreVanillaLayout(GameObject host)
    {
        LayoutGroup[] layouts = host.GetComponents<LayoutGroup>();
        for (int i = 0; i < layouts.Length; i++)
        {
            layouts[i].enabled = true;
        }

        ContentSizeFitter fitter = host.GetComponent<ContentSizeFitter>();
        if (fitter != null)
        {
            if (!VanillaFitterStates.TryGetValue(host, out bool enabled))
            {
                enabled = true;
            }
            fitter.enabled = enabled;
        }

        RectTransform rect = host.GetComponent<RectTransform>();
        if (rect != null)
        {
            if (VanillaContentSizes.TryGetValue(host, out Vector2 size))
            {
                rect.sizeDelta = size;
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        }

        VanillaContentSizes.Remove(host);
        VanillaFitterStates.Remove(host);
    }

    private static void BuildGrid(ChemFinisherUIManager ui)
    {
        GameObject host = ui.LayoutGroup;
        Transform content = host.transform;
        ChemFinishElement runtimeTemplate =
            content.GetComponentInChildren<ChemFinishElement>(true);
        CellStyle style = BuildStyle(ui, runtimeTemplate);
        SelectionOutlines.Clear();

        RectTransform hostRect = host.GetComponent<RectTransform>();
        if (hostRect == null)
        {
            return;
        }

        ScrollRect parentScroll = host.GetComponentInParent<ScrollRect>(true);
        if (!VanillaContentSizes.ContainsKey(host))
        {
            VanillaContentSizes[host] = hostRect.sizeDelta;
            ContentSizeFitter originalFitter = host.GetComponent<ContentSizeFitter>();
            VanillaFitterStates[host] = originalFitter != null && originalFitter.enabled;
        }

        ContentSizeFitter fitter = host.GetComponent<ContentSizeFitter>();
        if (fitter != null)
        {
            fitter.enabled = false;
        }

        LayoutGroup[] existing = host.GetComponents<LayoutGroup>();
        for (int i = 0; i < existing.Length; i++)
        {
            existing[i].enabled = false;
        }

        Canvas.ForceUpdateCanvases();

        // The vanilla OpenUI has already created its vertical elements. Remove
        // them; the grid builds fresh cells from scratch.
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            GameObject child = content.GetChild(i).gameObject;
            child.SetActive(false);
            UnityEngine.Object.Destroy(child);
        }

        int columns = Mathf.Clamp(ChemTabletExpansionMod.ColumnsPerRow, 4, 8);

        float availableWidth = hostRect.rect.width;
        if (availableWidth < 200f && parentScroll != null && parentScroll.viewport != null)
        {
            availableWidth = parentScroll.viewport.rect.width;
        }
        if (availableWidth < 200f)
        {
            availableWidth = FallbackWidth;
        }

        float cellWidth =
            (availableWidth - Padding * 2f - Spacing * (columns - 1)) / columns;
        cellWidth = Mathf.Max(48f, cellWidth);
        float cellHeight = Mathf.Clamp(cellWidth * 1.55f, 132f, 172f);

        bool chinese = IsChinese();
        List<ChemFinisherRecipe> recipes = ChemFinisherRecipe.chemFinisherRecipes;
        int created = 0;
        if (recipes != null)
        {
            for (int i = 0; i < recipes.Count; i++)
            {
                ChemFinisherRecipe recipe = recipes[i];
                if (recipe == null || string.IsNullOrEmpty(recipe.identifier))
                {
                    continue;
                }

                CreateCell(
                    ui, content, recipe, style, cellWidth, cellHeight,
                    chinese, created, columns);
                created++;
            }
        }

        int rows = created == 0 ? 0 : (created + columns - 1) / columns;
        float contentHeight = Padding * 2f + rows * cellHeight +
                              (rows > 1 ? (rows - 1) * Spacing : 0f);
        hostRect.sizeDelta = new Vector2(hostRect.sizeDelta.x, contentHeight);

        Canvas.ForceUpdateCanvases();

        if (parentScroll != null)
        {
            parentScroll.StopMovement();
            parentScroll.verticalNormalizedPosition = 1f;
        }

        ApplySelection(GetSelectedRecipe(ui));
    }

    private static string GetSelectedRecipe(ChemFinisherUIManager ui)
    {
        GameItem machine = ui != null ? ui.currentItem : null;
        if (machine != null && machine.IsTag("SELECTED_CHEM_ID_TAG"))
        {
            return machine.GetTagReadonly("SELECTED_CHEM_ID_TAG").GetString();
        }
        return null;
    }

    private static void ApplySelection(string recipeId)
    {
        foreach (KeyValuePair<string, Outline> entry in SelectionOutlines)
        {
            if (entry.Value != null)
            {
                entry.Value.enabled = string.Equals(
                    entry.Key, recipeId, StringComparison.Ordinal);
            }
        }
    }

    private static CellStyle BuildStyle(
        ChemFinisherUIManager ui,
        ChemFinishElement runtimeTemplate)
    {
        ChemFinishElement template = runtimeTemplate;
        if (ui.ChemFinishElementPrefab != null && template == null)
        {
            template = ui.ChemFinishElementPrefab.GetComponent<ChemFinishElement>();
        }

        TMP_FontAsset font = GetLocaleFont();
        if (font == null && template != null && template.displayTMP != null)
        {
            font = template.displayTMP.font;
        }
        if (font == null)
        {
            font = TMP_Settings.defaultFontAsset;
        }

        Color materialColor = FallbackMaterial;
        Color requirementColor = FallbackRequirement;
        if (template != null)
        {
            if (template.volumeTMP != null)
            {
                materialColor = template.volumeTMP.color;
            }
            if (template.dryTMP != null)
            {
                requirementColor = template.dryTMP.color;
            }
        }

        if (materialColor.a < 0.5f || materialColor.grayscale > 0.75f)
        {
            materialColor = FallbackMaterial;
        }
        if (requirementColor.a < 0.5f || requirementColor.grayscale > 0.75f)
        {
            requirementColor = FallbackRequirement;
        }

        return new CellStyle
        {
            Font = font,
            MaterialColor = materialColor,
            RequirementColor = requirementColor
        };
    }

    private static TMP_FontAsset GetLocaleFont()
    {
        try
        {
            FontLoader loader = FontLoader.Instance;
            UnityEngine.Localization.Locale locale = LocalizationSettings.SelectedLocale;
            if (loader != null && locale != null)
            {
                return loader.GetFontByRole(FontLoader.FontRole.Noto_Sans, locale);
            }
        }
        catch (Exception)
        {
            // Fall back to the prefab/default font below.
        }
        return null;
    }

    private static void CreateCell(
        ChemFinisherUIManager ui,
        Transform parent,
        ChemFinisherRecipe recipe,
        CellStyle style,
        float cellWidth,
        float cellHeight,
        bool chinese,
        int index,
        int columns)
    {
        GameObject cell = new GameObject("ChemTabletCell", typeof(RectTransform));
        cell.transform.SetParent(parent, false);

        RectTransform cellRect = cell.GetComponent<RectTransform>();
        int column = index % columns;
        int row = index / columns;
        cellRect.anchorMin = new Vector2(0f, 1f);
        cellRect.anchorMax = new Vector2(0f, 1f);
        cellRect.pivot = new Vector2(0f, 1f);
        cellRect.sizeDelta = new Vector2(cellWidth, cellHeight);
        cellRect.anchoredPosition = new Vector2(
            Padding + column * (cellWidth + Spacing),
            -(Padding + row * (cellHeight + Spacing)));

        Image background = cell.AddComponent<Image>();
        background.sprite = null;
        background.color = CellBackground;
        background.type = Image.Type.Simple;
        background.raycastTarget = true;

        Button button = cell.AddComponent<Button>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.None;

        ChemFinishElement element = cell.AddComponent<ChemFinishElement>();
        element.identifier = recipe.identifier;

        float iconBox = Mathf.Clamp(cellWidth * 0.52f, 28f, 92f);
        float nameSize = Mathf.Clamp(cellWidth * 0.155f, 11f, 15f);
        float materialSize = Mathf.Clamp(cellWidth * 0.135f, 10f, 13f);
        float requirementSize = Mathf.Clamp(cellWidth * 0.125f, 9f, 12f);

        Sprite mainIcon = GetItemIcon(recipe.identifier);
        if (mainIcon == null)
        {
            mainIcon = RenderHandler.unknownSprite;
        }

        float iconWidth = iconBox;
        float iconHeight = iconBox;
        if (mainIcon != null && mainIcon.rect.height > 0.01f)
        {
            float aspect = mainIcon.rect.width / mainIcon.rect.height;
            if (aspect >= 1f)
            {
                iconHeight = iconBox / aspect;
            }
            else
            {
                iconWidth = iconBox * aspect;
            }
        }

        float contentHeight = iconBox + 4f + 18f + 16f + 16f;
        float top = Mathf.Max(3f, (cellHeight - contentHeight) * 0.5f);
        float iconTop = -(top + (iconBox - iconHeight) * 0.5f);

        Image icon = CreateImage(
            cell.transform,
            "ItemIcon",
            mainIcon,
            new Vector2(iconWidth, iconHeight),
            new Vector2(0f, iconTop),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f));
        icon.preserveAspect = false;
        icon.raycastTarget = false;

        float nameTop = -(top + iconBox + 4f);

        TextMeshProUGUI nameText = CreateText(
            cell.transform, "Name", style.Font, nameSize, NameColor);
        SetTopStretch(nameText.rectTransform, nameTop, 18f);
        nameText.text = string.IsNullOrEmpty(recipe.displayName)
            ? recipe.identifier
            : recipe.displayName;

        TextMeshProUGUI materialText = CreateText(
            cell.transform, "Material", style.Font, materialSize, style.MaterialColor);
        SetTopStretch(materialText.rectTransform, nameTop - 18f, 16f);
        materialText.text = GetMaterialLine(recipe, chinese);

        TextMeshProUGUI requirementText = CreateText(
            cell.transform, "Requirement", style.Font, requirementSize, style.RequirementColor);
        SetTopStretch(requirementText.rectTransform, nameTop - 34f, 16f);
        requirementText.text = GetRequirementLine(recipe, chinese);

        element.displayTMP = nameText;
        element.volumeTMP = materialText;
        element.dryTMP = requirementText;

        string selectedId = recipe.identifier;
        button.onClick.AddListener(delegate
        {
            element.OnButtonClick();
            ApplySelection(selectedId);
        });

        Outline selection = cell.AddComponent<Outline>();
        selection.effectColor = SelectionColor;
        selection.effectDistance = new Vector2(2f, -2f);
        selection.useGraphicAlpha = false;
        selection.enabled = false;
        SelectionOutlines[recipe.identifier] = selection;
    }

    private static TextMeshProUGUI CreateText(
        Transform parent,
        string name,
        TMP_FontAsset font,
        float size,
        Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.font = font != null ? font : TMP_Settings.defaultFontAsset;
        text.fontSize = size;
        text.enableAutoSizing = true;
        text.fontSizeMin = 7f;
        text.fontSizeMax = size;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        text.text = "";
        return text;
    }

    private static void SetTopStretch(RectTransform rect, float top, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(-8f, height);
        rect.anchoredPosition = new Vector2(0f, top);
    }

    private static Image CreateImage(
        Transform parent,
        string name,
        Sprite sprite,
        Vector2 size,
        Vector2 position,
        Vector2 anchor,
        Vector2 pivot)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        Image image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private static Sprite GetItemIcon(string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            return RenderHandler.unknownSprite;
        }

        if (ItemIconCache.TryGetValue(identifier, out Sprite cached))
        {
            return cached;
        }

        Sprite sprite = null;
        GameItem item = null;
        try
        {
            item = DirectoryMaster.Create<GameItem>(identifier);
            if (item != null &&
                !string.IsNullOrEmpty(item.spriteAtlasPath) &&
                !string.IsNullOrEmpty(item.spritePath))
            {
                sprite = RenderHandler.TryLoadFromAtlas(item.spriteAtlasPath, item.spritePath);
            }
        }
        catch (Exception)
        {
            // Fall through to the unknown sprite.
        }
        finally
        {
            if (item != null)
            {
                try
                {
                    item.Destroy();
                }
                catch (Exception)
                {
                    // Icon extraction must never break the window.
                }
            }
        }

        if (sprite == null)
        {
            sprite = RenderHandler.unknownSprite;
        }

        ItemIconCache[identifier] = sprite;
        return sprite;
    }

    private static string GetMaterialLine(ChemFinisherRecipe recipe, bool chinese)
    {
        string identifier = recipe.activeIngredientId;
        string material;

        if (string.IsNullOrEmpty(identifier))
        {
            material = chinese ? "原料" : "Ingredient";
        }
        else if (Liquid.LiquidDict != null &&
                 Liquid.LiquidDict.TryGetValue(identifier, out Liquid liquid) &&
                 liquid != null &&
                 !string.IsNullOrEmpty(liquid.displayName))
        {
            material = liquid.displayName;
        }
        else
        {
            material = identifier;
        }

        string unit = "mL";
        string volume = (recipe.requiredVolume / 1000f).ToString(
            "0.00", CultureInfo.InvariantCulture);
        return material + " " + volume + unit;
    }

    private static string GetRequirementLine(ChemFinisherRecipe recipe, bool chinese)
    {
        if (recipe.isRequireDry)
        {
            return chinese ? "不能含有液体原料" : "No liquid ingredients";
        }
        return chinese ? "可含有液体原料" : "Liquid ingredients allowed";
    }

    private static bool IsChinese()
    {
        string code = LocHelper.GetCurrentLocaleCode();
        if (!string.IsNullOrEmpty(code))
        {
            return code.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
        }

        SystemLanguage language = Application.systemLanguage;
        return language == SystemLanguage.Chinese ||
               language == SystemLanguage.ChineseSimplified ||
               language == SystemLanguage.ChineseTraditional;
    }
}
