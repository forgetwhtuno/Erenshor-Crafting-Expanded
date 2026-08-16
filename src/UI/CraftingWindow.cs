using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ErenshorCraftingExpanded
{
    internal static class CraftingWindow
    {
        internal const int CanvasSortOrder = 523;
        private static GameObject _root;
        private static RectTransform _panel;
        private static RectTransform _bodyRect;
        private static RectTransform _footer;
        private static Button _collapse;
        private static TextMeshProUGUI _collapseLabel;
        private static bool _collapsed;
        private static RectTransform _commissionRoot;
        private static RectTransform _resourceContent;
        private static RectTransform _activeMaterialContent;
        private static RectTransform _recipeContent;
        private static RectTransform _recipeRecoveryRow;
        private static TextMeshProUGUI _craftingProgress;
        private static TextMeshProUGUI _foragingProgress;
        private static TextMeshProUGUI _nextExploration;
        private static TextMeshProUGUI _activeForgeTitle;
        private static TextMeshProUGUI _activeForgeStatus;
        private static TextMeshProUGUI _hotkey;
        private static TextMeshProUGUI _recipeSummary;
        private static TextMeshProUGUI _recipePersistence;
        private static TextMeshProUGUI _recipeMessage;
        private static TextMeshProUGUI _commissionText;
        private static Button _accept;
        private static Button _decline;
        private static Button _pin;
        private static TextMeshProUGUI _pinLabel;
        private static Button _enabledButton;
        private static TextMeshProUGUI _enabledLabel;
        private static Button _foragingButton;
        private static TextMeshProUGUI _foragingLabel;
        private static RetainedPosition _position;
        private static string _commissionSignature = string.Empty;
        private static string _resourceSignature = string.Empty;
        private static string _activeForgeSignature = string.Empty;
        private static string _recipeSignature = string.Empty;
        private static float _nextKnowledgeRefresh;

        internal static void Initialize(float x, float y, Action<float, float> persist)
        {
            Dispose();
            _root = RetainedUiKit.CreateCanvas("ErenshorCraftingCanvas", CanvasSortOrder);
            RectTransform canvas = _root.GetComponent<RectTransform>();
            _panel = RetainedUiKit.CreateRect("CraftingPanel", canvas);
            RetainedUiKit.AnchorBottomLeft(_panel, 0f, 0f, CraftingPanelLayoutPolicy.Width,
                CraftingPanelLayoutPolicy.HeightFor(false, Screen.height));
            RetainedUiKit.AddImage(_panel, RetainedUiKit.Panel);
            // The panel and body viewport are both clipped. Variable knowledge content may become
            // taller than the window, but it can never render outside the retained dark surface.
            _panel.gameObject.AddComponent<RectMask2D>();
            _panel.gameObject.AddComponent<CanvasGroup>();

            RectTransform header = RetainedUiKit.CreateRect("Header", _panel);
            RetainedUiKit.AnchorTopStretch(header, 0f, 0f, 0f, CraftingPanelLayoutPolicy.HeaderHeight);
            RetainedUiKit.AddImage(header, RetainedUiKit.Header);
            TextMeshProUGUI title = RetainedUiKit.AddLabel("Title", header, "CRAFTING", 15f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            RetainedUiKit.Stretch(title.rectTransform, 10f, 0f, 104f, 0f);
            AddHeaderButton(header, "Collapse", "▲", -70f, ToggleCollapsed, out _collapse);
            _collapseLabel = _collapse.GetComponentInChildren<TextMeshProUGUI>();
            AddHeaderButton(header, "Reset", "R", -38f, ResetPosition);
            AddHeaderButton(header, "Close", "X", -6f, delegate { CraftingUiStateMachine.Close(); });
            RetainedUiKit.AddDragSurface("DragSurface", header, _panel, 104f,
                delegate { if (_position != null) _position.DragCompleted(_panel); });

            RectTransform bodyViewport;
            RectTransform content;
            ScrollRect bodyScroll = RetainedUiKit.AddScrollRect("BodyScroll", _panel, false, true, out bodyViewport, out content);
            RectTransform bodyRect = bodyScroll.GetComponent<RectTransform>();
            _bodyRect = bodyRect;
            bodyRect.anchorMin = Vector2.zero;
            bodyRect.anchorMax = Vector2.one;
            bodyRect.pivot = new Vector2(0.5f, 0.5f);
            bodyRect.offsetMin = new Vector2(CraftingPanelLayoutPolicy.OuterInset, CraftingPanelLayoutPolicy.BodyBottomInset());
            bodyRect.offsetMax = new Vector2(-CraftingPanelLayoutPolicy.OuterInset, -CraftingPanelLayoutPolicy.BodyTopInset());
            VerticalLayoutGroup contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(7, 7, 7, 7);
            contentLayout.spacing = CraftingPanelLayoutPolicy.BodySpacing;
            contentLayout.childAlignment = TextAnchor.UpperLeft;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            ContentSizeFitter bodyFitter = content.gameObject.AddComponent<ContentSizeFitter>();
            bodyFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            AddSectionHeader(content, "PROFESSIONS");
            _craftingProgress = AddLine(content, "CRAFTING  •  Waiting for active character", true, CraftingPanelLayoutPolicy.ProgressLineHeight);
            _foragingProgress = AddLine(content, "FORAGING  •  Waiting for active character", true, CraftingPanelLayoutPolicy.ProgressLineHeight);

            AddSectionHeader(content, "KNOWN RESOURCES");
            _resourceContent = AddDynamicVerticalContent("ResourceRows", content);
            _nextExploration = AddLine(content, "", false, CraftingPanelLayoutPolicy.HintLineHeight);
            _nextExploration.color = RetainedUiKit.Muted;

            AddSectionHeader(content, "FORGE NOW");
            _activeForgeTitle = AddLine(content, "No recipe loaded", true, CraftingPanelLayoutPolicy.ProgressLineHeight);
            _activeForgeStatus = AddLine(content, "Open a forge and load a template to inspect materials.", false, CraftingPanelLayoutPolicy.HintLineHeight);
            _activeForgeStatus.color = RetainedUiKit.Muted;
            _activeMaterialContent = AddDynamicVerticalContent("ActiveMaterials", content);
            _hotkey = AddLine(content, "", false, CraftingPanelLayoutPolicy.ProgressLineHeight);
            _hotkey.color = RetainedUiKit.Muted;
            _hotkey.gameObject.SetActive(false);

            _recipeSummary = AddLine(content, CraftingKnowledgePresentationPolicy.BuildRecipeSummary(0, 0), true, CraftingPanelLayoutPolicy.SectionHeaderHeight);
            _recipeSummary.color = RetainedUiKit.Edge;
            _recipePersistence = AddLine(content, "", false, CraftingPanelLayoutPolicy.ProgressLineHeight);
            _recipePersistence.color = RetainedUiKit.Muted;
            _recipePersistence.gameObject.SetActive(false);

            _recipeContent = AddDynamicVerticalContent("RecipeRows", content);

            _recipeRecoveryRow = RetainedUiKit.AddHorizontalRow("RecipeRecovery", content, 30f, 6f);
            RetainedUiKit.AddButton("RestoreAll", _recipeRecoveryRow, "Restore Missing Templates", delegate
            {
                RecipeOwnershipController.RestoreAllSafe();
                _nextKnowledgeRefresh = 0f;
            }, 188f, 26f, false);
            _recipeRecoveryRow.gameObject.SetActive(false);

            _recipeMessage = AddLine(content, "", false, CraftingPanelLayoutPolicy.HintLineHeight);
            _recipeMessage.color = RetainedUiKit.Muted;
            _recipeMessage.gameObject.SetActive(false);

            _commissionRoot = RetainedUiKit.CreateRect("Commission", content);
            VerticalLayoutGroup commissionLayout = _commissionRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            commissionLayout.padding = new RectOffset(6, 6, 6, 6);
            commissionLayout.spacing = 4f;
            commissionLayout.childAlignment = TextAnchor.UpperLeft;
            commissionLayout.childControlWidth = true;
            commissionLayout.childControlHeight = true;
            commissionLayout.childForceExpandWidth = true;
            commissionLayout.childForceExpandHeight = false;
            LayoutElement cle = _commissionRoot.gameObject.AddComponent<LayoutElement>();
            cle.minHeight = 96f; cle.preferredHeight = 96f; cle.flexibleWidth = 1f;
            _commissionText = AddLine(_commissionRoot, "No active request.", false, 48f);
            RectTransform buttons = RetainedUiKit.AddHorizontalRow("CommissionActions", _commissionRoot, 28f, 6f);
            _accept = RetainedUiKit.AddButton("Accept", buttons, "Accept", delegate { CommissionController.Accept(); }, 88f, 26f, false);
            _decline = RetainedUiKit.AddButton("Decline", buttons, "Decline", delegate { CommissionController.Decline(); }, 88f, 26f, false);

            RectTransform footer = RetainedUiKit.CreateRect("Footer", _panel);
            _footer = footer;
            footer.anchorMin = new Vector2(0f, 0f);
            footer.anchorMax = new Vector2(1f, 0f);
            footer.pivot = new Vector2(0.5f, 0f);
            footer.offsetMin = new Vector2(CraftingPanelLayoutPolicy.OuterInset, CraftingPanelLayoutPolicy.OuterInset);
            footer.offsetMax = new Vector2(-CraftingPanelLayoutPolicy.OuterInset, CraftingPanelLayoutPolicy.OuterInset + CraftingPanelLayoutPolicy.FooterHeight);
            VerticalLayoutGroup footerLayout = footer.gameObject.AddComponent<VerticalLayoutGroup>();
            footerLayout.padding = new RectOffset(6, 6, 6, 6);
            footerLayout.spacing = 4f;
            footerLayout.childAlignment = TextAnchor.UpperLeft;
            footerLayout.childControlWidth = true;
            footerLayout.childControlHeight = true;
            footerLayout.childForceExpandWidth = true;
            footerLayout.childForceExpandHeight = false;

            RectTransform settingsRow = RetainedUiKit.AddHorizontalRow("Settings", footer, 30f, 6f);
            _enabledButton = RetainedUiKit.AddButton("Enabled", settingsRow, "", delegate { CraftingController.SetEnabled(!(CraftingConfig.EnableMod != null && CraftingConfig.EnableMod.Value)); }, 132f, 26f, false);
            _enabledLabel = _enabledButton.GetComponentInChildren<TextMeshProUGUI>();
            _foragingButton = RetainedUiKit.AddButton("Foraging", settingsRow, "", delegate { CraftingController.SetForagingEnabled(!(ForagingConfig.EnableForaging != null && ForagingConfig.EnableForaging.Value)); }, 132f, 26f, false);
            _foragingLabel = _foragingButton.GetComponentInChildren<TextMeshProUGUI>();

            RectTransform bottom = RetainedUiKit.AddHorizontalRow("Bottom", footer, 30f, 6f);
            _pin = RetainedUiKit.AddButton("Pin", bottom, "Pin", TogglePin, 78f, 26f, false);
            _pinLabel = _pin.GetComponentInChildren<TextMeshProUGUI>();
            RetainedUiKit.AddButton("Close", bottom, "Close", delegate { CraftingUiStateMachine.Close(); }, 78f, 26f, false);

            TextMeshProUGUI hint = AddLine(footer, "Known recipes persist. Physical templates can be replaced when safely missing.", false, 34f);
            hint.color = RetainedUiKit.Muted;

            _position = new RetainedPosition(x, y, 0.18f, 0.40f, persist);
            _position.Resolve(_panel);
            SetCollapsed(false);
            _root.SetActive(false);
        }

        internal static void Tick(bool visible)
        {
            if (_root == null) return;
            if (_root.activeSelf != visible) _root.SetActive(visible);
            if (!visible) return;
            if (_position != null) _position.Resolve(_panel);

            if (_collapsed)
            {
                ResizeForCommissionState(false);
                return;
            }

            CraftingProgress progress = CraftingController.Progress ?? new CraftingProgress();
            if (CraftingController.CharacterScopeResolved)
            {
                int need = SmithingXpCurve.XpToNextLevel(progress.Level);
                _craftingProgress.text = need > 0
                    ? "CRAFTING  •  Lv " + progress.Level.ToString() + "  •  " + progress.Xp.ToString() + " / " + need.ToString() + " XP"
                    : "CRAFTING  •  Lv " + progress.Level.ToString() + "  •  Max level";
            }
            else _craftingProgress.text = "CRAFTING  •  Waiting for active character";

            if (ForagingKnowledge.IsReady)
            {
                int forageNeed = ForagingKnowledge.XpToNext;
                _foragingProgress.text = forageNeed > 0
                    ? "FORAGING  •  Lv " + ForagingKnowledge.CurrentLevel.ToString() + "  •  " + ForagingKnowledge.CurrentXp.ToString() + " / " + forageNeed.ToString() + " XP"
                    : "FORAGING  •  Lv " + ForagingKnowledge.CurrentLevel.ToString() + "  •  Max level";
            }
            else _foragingProgress.text = "FORAGING  •  Waiting for active character";

            if (_enabledLabel != null) _enabledLabel.text = CraftingPanelLayoutPolicy.BoolButtonText("Crafting", CraftingConfig.EnableMod != null && CraftingConfig.EnableMod.Value);
            if (_foragingLabel != null) _foragingLabel.text = CraftingPanelLayoutPolicy.BoolButtonText("Foraging", ForagingConfig.EnableForaging != null && ForagingConfig.EnableForaging.Value);

            KeyCode hotkey = CraftingConfig.CraftHotkey != null ? CraftingConfig.CraftHotkey.Value : KeyCode.None;
            bool showHotkey = hotkey != KeyCode.None;
            if (_hotkey.gameObject.activeSelf != showHotkey) _hotkey.gameObject.SetActive(showHotkey);
            if (showHotkey) _hotkey.text = "Craft hotkey: " + hotkey.ToString();

            if (Time.unscaledTime >= _nextKnowledgeRefresh)
            {
                _nextKnowledgeRefresh = Time.unscaledTime + 0.5f;
                RefreshKnowledge(progress.Level);
            }

            bool commissionsEnabled = CraftingConfig.EnableCraftingRequests != null && CraftingConfig.EnableCraftingRequests.Value;
            if (_commissionRoot != null && _commissionRoot.gameObject.activeSelf != commissionsEnabled)
                _commissionRoot.gameObject.SetActive(commissionsEnabled);
            ResizeForCommissionState(commissionsEnabled);

            CraftingCommission commission = commissionsEnabled ? CommissionController.Current : null;
            string signature = commissionsEnabled ? CommissionSignature(commission) : "disabled";
            if (!string.Equals(signature, _commissionSignature, StringComparison.Ordinal))
            {
                _commissionSignature = signature;
                if (commissionsEnabled) BindCommission(commission);
            }
            bool pinned = CraftingUiStateMachine.Current == CraftingUiState.PinnedOpen;
            if (_pinLabel != null) _pinLabel.text = pinned ? "Unpin" : "Pin";
        }

        private static void RefreshKnowledge(int craftingLevel)
        {
            RefreshResources();
            RefreshActiveForge();
            RefreshRecipeBook(craftingLevel);
        }

        private static void RefreshResources()
        {
            if (!ForagingKnowledge.IsReady)
            {
                if (!string.Equals(_resourceSignature, "waiting", StringComparison.Ordinal))
                {
                    _resourceSignature = "waiting";
                    RetainedUiKit.ClearChildren(_resourceContent);
                    TextMeshProUGUI waiting = AddRecipeLabel(_resourceContent, "Resource knowledge is waiting for the active character.", false, CraftingPanelLayoutPolicy.ResourceRowHeight);
                    waiting.color = RetainedUiKit.Muted;
                }
                _nextExploration.text = string.Empty;
                return;
            }

            ForagingKnowledgeSnapshot snapshot = ForagingKnowledge.GetSnapshot();
            bool coveredEnabled = ForagingConfig.ExperimentalCoveredResources != null && ForagingConfig.ExperimentalCoveredResources.Value;
            List<CraftingResourceDisplayModel> rows = new List<CraftingResourceDisplayModel>();
            System.Text.StringBuilder signatureBuilder = new System.Text.StringBuilder();
            for (int i = 0; i < snapshot.Resources.Count; i++)
            {
                ForagingResourceKnowledgeSnapshot knowledge = snapshot.Resources[i];
                ForageResourceDefinition definition = ForageResourceCatalog.FindByKnowledgeKey(knowledge.Key);
                CraftingResourceDisplayModel row = CraftingKnowledgePresentationPolicy.BuildResourceRow(
                    definition, snapshot.Level, knowledge.Discovered, coveredEnabled);
                if (row == null) continue;
                rows.Add(row);
                signatureBuilder.Append(row.Key).Append(':').Append(row.StateText).Append(':').Append(row.DetailText).Append('|');
            }

            string signature = signatureBuilder.ToString();
            if (!string.Equals(signature, _resourceSignature, StringComparison.Ordinal))
            {
                _resourceSignature = signature;
                RebuildResourceRows(rows);
            }
            _nextExploration.text = "NEXT  •  " + CraftingKnowledgePresentationPolicy.BuildNextExplorationHint(rows);
        }

        private static void RebuildResourceRows(IList<CraftingResourceDisplayModel> rows)
        {
            RetainedUiKit.ClearChildren(_resourceContent);
            if (rows == null || rows.Count == 0)
            {
                TextMeshProUGUI none = AddRecipeLabel(_resourceContent, "No foraging resources are currently available.", false, CraftingPanelLayoutPolicy.ResourceRowHeight);
                none.color = RetainedUiKit.Muted;
                return;
            }
            for (int i = 0; i < rows.Count; i++)
            {
                CraftingResourceDisplayModel row = rows[i];
                if (row == null) continue;
                AddStateRow(_resourceContent, "Resource", row.DisplayName, row.DetailText, row.StateText, row.Discovered);
            }
        }

        private static void RefreshActiveForge()
        {
            CraftRecipeSnapshot recipe = CraftingController.ActiveRecipeSnapshot;
            bool special = recipe != null && GameCraftingApi.IsSpecialCombineTemplate(recipe.TemplateItemId);
            CraftingActiveRecipeDisplayModel model = CraftingKnowledgePresentationPolicy.BuildActiveRecipe(
                recipe,
                CraftingController.ActiveCraftingAvailability,
                CraftingController.ActiveFuelSourceUnits,
                CraftingController.LastCraftableCount,
                special);

            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            builder.Append(model.HasRecipe ? '1' : '0').Append('|').Append(model.UsesNativeSpecialRules ? '1' : '0').Append('|');
            builder.Append(recipe == null ? string.Empty : recipe.TemplateItemId).Append('|').Append(model.Title).Append('|').Append(model.StatusText).Append('|');
            for (int i = 0; i < model.Materials.Count; i++)
            {
                CraftingMaterialDisplayModel material = model.Materials[i];
                builder.Append(material.ItemId).Append(':').Append(material.Available).Append('/').Append(material.Required).Append('|');
            }
            string signature = builder.ToString();
            if (string.Equals(signature, _activeForgeSignature, StringComparison.Ordinal)) return;
            _activeForgeSignature = signature;

            if (recipe == null)
                _activeForgeTitle.text = model.Title;
            else
            {
                string templateName = string.IsNullOrEmpty(recipe.TemplateItemName) ? "Loaded template" : recipe.TemplateItemName;
                _activeForgeTitle.text = string.IsNullOrEmpty(recipe.OutputItemName)
                    ? templateName
                    : templateName + "  →  " + recipe.OutputItemName;
            }
            _activeForgeStatus.text = model.StatusText;
            _activeForgeStatus.color = model.HasRecipe && CraftingController.LastCraftableCount > 0 ? RetainedUiKit.Edge : RetainedUiKit.Muted;
            RebuildMaterialRows(model.Materials);
        }

        private static void RebuildMaterialRows(IList<CraftingMaterialDisplayModel> materials)
        {
            RetainedUiKit.ClearChildren(_activeMaterialContent);
            if (materials == null || materials.Count == 0) return;
            for (int i = 0; i < materials.Count; i++)
            {
                CraftingMaterialDisplayModel material = materials[i];
                if (material == null) continue;
                AddMaterialRow(_activeMaterialContent, material);
            }
        }

        private static void AddMaterialRow(RectTransform parent, CraftingMaterialDisplayModel material)
        {
            RectTransform row = RetainedUiKit.AddHorizontalRow("Material", parent, CraftingPanelLayoutPolicy.MaterialRowHeight, 6f);
            TextMeshProUGUI name = RetainedUiKit.AddLabel("Name", row, material.DisplayName, 10.5f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            LayoutElement nameLayout = name.gameObject.AddComponent<LayoutElement>();
            nameLayout.flexibleWidth = 1f;
            nameLayout.minHeight = CraftingPanelLayoutPolicy.MaterialRowHeight;
            TextMeshProUGUI quantity = RetainedUiKit.AddLabel("Quantity", row,
                material.Available.ToString() + " / " + material.Required.ToString(), 10.5f, FontStyles.Bold, TextAlignmentOptions.MidlineRight);
            LayoutElement quantityLayout = quantity.gameObject.AddComponent<LayoutElement>();
            quantityLayout.preferredWidth = 82f;
            quantityLayout.minWidth = 82f;
            quantityLayout.minHeight = CraftingPanelLayoutPolicy.MaterialRowHeight;
            quantity.color = material.Sufficient ? RetainedUiKit.Edge : RetainedUiKit.Muted;
        }

        private static void RefreshRecipeBook(int craftingLevel)
        {
            RecipeBookSnapshot book = RecipeOwnershipController.BuildBookSnapshot(craftingLevel);
            _recipeSummary.text = CraftingKnowledgePresentationPolicy.BuildRecipeSummary(book.KnownCount, book.TotalCount);
            bool hasRecipes = book.TotalCount > 0;
            if (_recipePersistence.gameObject.activeSelf != hasRecipes) _recipePersistence.gameObject.SetActive(hasRecipes);
            if (hasRecipes)
            {
                _recipePersistence.text = book.CharacterPersistenceAvailable
                    ? "Recipe knowledge saves per character."
                    : "Recipe knowledge is session-only until the active character can be verified.";
            }

            bool hasMessage = !string.IsNullOrEmpty(book.LastPlayerMessage);
            if (_recipeMessage.gameObject.activeSelf != hasMessage) _recipeMessage.gameObject.SetActive(hasMessage);
            if (hasMessage) _recipeMessage.text = book.LastPlayerMessage;

            bool canRestore = false;
            for (int i = 0; i < book.Known.Count; i++)
                if (book.Known[i] != null && book.Known[i].CanRestore) { canRestore = true; break; }
            if (_recipeRecoveryRow.gameObject.activeSelf != canRestore) _recipeRecoveryRow.gameObject.SetActive(canRestore);

            string signature = BuildRecipeSignature(book);
            if (string.Equals(signature, _recipeSignature, StringComparison.Ordinal)) return;
            _recipeSignature = signature;
            RebuildRecipeRows(book);
        }

        private static string BuildRecipeSignature(RecipeBookSnapshot book)
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            builder.Append(book.KnownCount).Append('|').Append(book.TotalCount).Append('|').Append(book.CharacterPersistenceAvailable ? '1' : '0').Append('|');
            for (int i = 0; i < book.Known.Count; i++)
            {
                RecipeBookRowModel row = book.Known[i];
                builder.Append(row.StableRecipeId).Append(':').Append((int)row.TemplateLocation).Append(':').Append(row.CanRestore ? '1' : '0').Append(':').Append(row.StatusText).Append('|');
            }
            builder.Append('#');
            for (int i = 0; i < book.Locked.Count; i++)
            {
                RecipeBookRowModel row = book.Locked[i];
                builder.Append(row.StableRecipeId).Append(':').Append(row.LockReason).Append('|');
            }
            return builder.ToString();
        }

        private static void RebuildRecipeRows(RecipeBookSnapshot book)
        {
            if (_recipeContent == null) return;
            RetainedUiKit.ClearChildren(_recipeContent);
            if (book.TotalCount == 0)
            {
                TextMeshProUGUI empty = AddRecipeLabel(_recipeContent,
                    "No expanded recipes are registered yet.\nNative Smithing recipes still work normally.", false, 46f);
                empty.color = RetainedUiKit.Muted;
                return;
            }

            if (book.Known.Count > 0)
            {
                AddRecipeSectionHeader(_recipeContent, "KNOWN");
                for (int i = 0; i < book.Known.Count; i++) AddKnownRecipeRow(_recipeContent, book.Known[i]);
            }
            if (book.Known.Count == 0)
            {
                TextMeshProUGUI none = AddRecipeLabel(_recipeContent, "No expanded recipes learned yet.", false, 34f);
                none.color = RetainedUiKit.Muted;
            }

            if (book.Locked.Count > 0)
            {
                AddRecipeSectionHeader(_recipeContent, "LOCKED");
                for (int i = 0; i < book.Locked.Count; i++) AddLockedRecipeRow(_recipeContent, book.Locked[i]);
            }
        }

        private static void AddKnownRecipeRow(RectTransform parent, RecipeBookRowModel row)
        {
            RectTransform item = RetainedUiKit.AddHorizontalRow("KnownRecipe", parent, CraftingPanelLayoutPolicy.KnownRecipeRowHeight, 6f);
            HorizontalLayoutGroup layout = item.GetComponent<HorizontalLayoutGroup>();
            if (layout != null) { layout.childControlWidth = true; layout.childForceExpandWidth = false; }
            TextMeshProUGUI label = RetainedUiKit.AddLabel("Recipe", item,
                (row.DisplayName ?? "Recipe") + "\nKNOWN  •  " + (row.StatusText ?? string.Empty), 11f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            LayoutElement le = label.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f; le.minWidth = 220f; le.minHeight = CraftingPanelLayoutPolicy.KnownRecipeRowHeight - 2f;
            if (row.CanRestore)
            {
                string recipeId = row.StableRecipeId;
                RetainedUiKit.AddButton("Restore", item, "Restore", delegate
                {
                    RecipeOwnershipController.Restore(recipeId);
                    _nextKnowledgeRefresh = 0f;
                }, 72f, 26f, false);
            }
        }

        private static void AddLockedRecipeRow(RectTransform parent, RecipeBookRowModel row)
        {
            TextMeshProUGUI label = AddRecipeLabel(parent,
                (row.DisplayName ?? "Recipe") + "\nLOCKED  •  " + (row.LockReason ?? "Not yet learned"), false, CraftingPanelLayoutPolicy.LockedRecipeRowHeight);
            label.color = RetainedUiKit.Muted;
        }

        private static void AddRecipeSectionHeader(RectTransform parent, string text)
        {
            TextMeshProUGUI label = AddRecipeLabel(parent, text, true, CraftingPanelLayoutPolicy.SectionHeaderHeight);
            label.color = RetainedUiKit.Edge;
        }

        private static void AddSectionHeader(RectTransform parent, string text)
        {
            TextMeshProUGUI label = AddLine(parent, text, true, CraftingPanelLayoutPolicy.SectionHeaderHeight);
            label.color = RetainedUiKit.Edge;
        }

        private static RectTransform AddDynamicVerticalContent(string name, RectTransform parent)
        {
            RectTransform content = RetainedUiKit.CreateRect(name, parent);
            VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(5, 5, 2, 2);
            layout.spacing = 3f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return content;
        }

        private static void AddStateRow(RectTransform parent, string name, string title, string detail, string state, bool positive)
        {
            RectTransform row = RetainedUiKit.AddHorizontalRow(name, parent, CraftingPanelLayoutPolicy.ResourceRowHeight, 6f);
            TextMeshProUGUI left = RetainedUiKit.AddLabel("Detail", row,
                (title ?? "Resource") + (string.IsNullOrEmpty(detail) ? string.Empty : "\n" + detail), 10.5f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            LayoutElement leftLayout = left.gameObject.AddComponent<LayoutElement>();
            leftLayout.flexibleWidth = 1f;
            leftLayout.minHeight = CraftingPanelLayoutPolicy.ResourceRowHeight;
            TextMeshProUGUI right = RetainedUiKit.AddLabel("State", row, state ?? string.Empty, 9.5f, FontStyles.Bold, TextAlignmentOptions.MidlineRight);
            LayoutElement rightLayout = right.gameObject.AddComponent<LayoutElement>();
            rightLayout.preferredWidth = 104f;
            rightLayout.minWidth = 104f;
            rightLayout.minHeight = CraftingPanelLayoutPolicy.ResourceRowHeight;
            right.color = positive ? RetainedUiKit.Edge : RetainedUiKit.Muted;
        }

        private static TextMeshProUGUI AddRecipeLabel(RectTransform parent, string text, bool bold, float height)
        {
            TextMeshProUGUI label = RetainedUiKit.AddLabel("RecipeLine", parent, text, 11f, bold ? FontStyles.Bold : FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            LayoutElement le = label.gameObject.AddComponent<LayoutElement>();
            le.minHeight = height; le.preferredHeight = height; le.flexibleWidth = 1f;
            return label;
        }

        private static void ResizeForCommissionState(bool commissionsEnabled)
        {
            if (_panel == null) return;
            float targetHeight = _collapsed
                ? CraftingPanelLayoutPolicy.HeightForCollapsed()
                : CraftingPanelLayoutPolicy.HeightFor(commissionsEnabled, Screen.height);
            Vector2 size = _panel.sizeDelta;
            if (Mathf.Abs(size.y - targetHeight) < 0.01f) return;
            size.y = targetHeight;
            _panel.sizeDelta = size;
            if (_position != null) _position.Clamp(_panel);
        }

        private static void BindCommission(CraftingCommission commission)
        {
            bool active = commission != null && (commission.State == CommissionState.Offered || commission.State == CommissionState.Accepted);
            if (!active)
            {
                _commissionText.text = "Current Request\nNo active request.";
                _accept.gameObject.SetActive(false); _decline.gameObject.SetActive(false); return;
            }
            _commissionText.text = "Current Request\n" + (commission.SimName ?? "Sim") + " needs:\n" + (commission.RequestedItemName ?? "Unknown item") +
                (commission.State == CommissionState.Accepted ? "\n(accepted)" : string.Empty);
            bool offered = commission.State == CommissionState.Offered;
            _accept.gameObject.SetActive(offered); _decline.gameObject.SetActive(offered);
        }

        private static string CommissionSignature(CraftingCommission c)
        {
            if (c == null) return "none";
            return (c.SimName ?? "") + "|" + (c.RequestedItemName ?? "") + "|" + c.State.ToString();
        }

        private static void TogglePin()
        {
            CraftingUiStateMachine.SetPinned(CraftingUiStateMachine.Current != CraftingUiState.PinnedOpen);
        }

        private static void ToggleCollapsed()
        {
            SetCollapsed(!_collapsed);
        }

        private static void SetCollapsed(bool collapsed)
        {
            _collapsed = collapsed;
            if (_bodyRect != null) _bodyRect.gameObject.SetActive(!collapsed);
            if (_footer != null) _footer.gameObject.SetActive(!collapsed);
            if (_collapseLabel != null) _collapseLabel.text = collapsed ? "▼" : "▲";
            bool commissionsEnabled = !collapsed && CraftingConfig.EnableCraftingRequests != null && CraftingConfig.EnableCraftingRequests.Value;
            ResizeForCommissionState(commissionsEnabled);
        }

        internal static void ResetTransientState()
        {
            SuiteDragHandler.ForceReleaseIfOwned();
            _commissionSignature = string.Empty;
            _resourceSignature = string.Empty;
            _activeForgeSignature = string.Empty;
            _recipeSignature = string.Empty;
            _nextKnowledgeRefresh = 0f;
        }

        internal static void ResetPosition() { if (_position != null) _position.Reset(_panel); }

        internal static void Dispose()
        {
            SuiteDragHandler.ForceReleaseIfOwned();
            RetainedUiKit.DestroyRoot(ref _root);
            _panel = null;
            _bodyRect = null;
            _footer = null;
            _collapse = null;
            _collapseLabel = null;
            _collapsed = false;
            _commissionRoot = null;
            _resourceContent = null;
            _activeMaterialContent = null;
            _recipeContent = null;
            _recipeRecoveryRow = null;
            _craftingProgress = null;
            _foragingProgress = null;
            _nextExploration = null;
            _activeForgeTitle = null;
            _activeForgeStatus = null;
            _hotkey = null;
            _recipeSummary = null;
            _recipePersistence = null;
            _recipeMessage = null;
            _commissionText = null;
            _accept = null;
            _decline = null;
            _pin = null;
            _pinLabel = null;
            _enabledButton = null;
            _enabledLabel = null;
            _foragingButton = null;
            _foragingLabel = null;
            _position = null;
            _commissionSignature = string.Empty;
            _resourceSignature = string.Empty;
            _activeForgeSignature = string.Empty;
            _recipeSignature = string.Empty;
            _nextKnowledgeRefresh = 0f;
        }

        private static TextMeshProUGUI AddLine(RectTransform parent, string text, bool bold, float height)
        {
            TextMeshProUGUI label = RetainedUiKit.AddLabel("Line", parent, text, 11f, bold ? FontStyles.Bold : FontStyles.Normal, TextAlignmentOptions.TopLeft);
            LayoutElement le = label.gameObject.AddComponent<LayoutElement>();
            le.minHeight = height; le.preferredHeight = height; le.flexibleWidth = 1f;
            return label;
        }

        private static void AddHeaderButton(RectTransform header, string name, string label, float right, Action action)
        {
            Button ignored;
            AddHeaderButton(header, name, label, right, action, out ignored);
        }

        private static void AddHeaderButton(RectTransform header, string name, string label, float right, Action action, out Button button)
        {
            Button b = RetainedUiKit.AddButton(name, header, label, action, 28f, 24f, false);
            RectTransform r = b.GetComponent<RectTransform>();
            LayoutElement le = r.GetComponent<LayoutElement>();
            if (le != null) UnityEngine.Object.DestroyImmediate(le);
            r.anchorMin = r.anchorMax = new Vector2(1f, 0.5f);
            r.pivot = new Vector2(1f, 0.5f);
            r.anchoredPosition = new Vector2(right, 0f);
            r.sizeDelta = new Vector2(28f, 24f);
            button = b;
        }
    }
}
