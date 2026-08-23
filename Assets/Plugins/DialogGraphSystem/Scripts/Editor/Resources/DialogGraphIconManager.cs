using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using DialogSystem.Runtime.Utils;

namespace DialogSystem.EditorTools.Resources
{
    public enum DialogGraphIconId
    {
        ToolbarOpen,
        ToolbarSave,
        ToolbarValidate,
        ToolbarPreview,
        ToolbarSettings,
        ToolbarAutoLayout,

        NodeStart,
        NodeEnd,
        NodeDialog,
        NodeChoice,
        NodeAction,
        NodeCondition,
        NodeNarration,
        NodeAiDraft,
        NodeOutcome,

        AiGenerate,
        AiRewrite,
        AiSuggestChoices,
        AiContinueBranch,
        AiHistory,
        AiProvider,

        StatusSuccess,
        StatusWarning,
        StatusError,
        StatusInfo,
        StatusLocked,
        StatusConnected,
        StatusDisconnected,

        ActionAdd,
        ActionDelete,
        ActionDuplicate,
        ActionEdit,
        ActionApply,
        ActionCancel,
        ActionExpand,
        ActionCollapse,
        ActionMore
    }

    /// <summary>
    /// Central editor-only icon lookup and UI Toolkit helpers for Dialogue Graph System.
    /// </summary>
    public static class DialogGraphIconManager
    {
        private const string IconFolderPath = "Assets/DialogGraphSystem/Resources/EditorIcons";
        private const string BackgroundImageChildName = "dgs-icon-image";

        private static readonly Dictionary<DialogGraphIconId, string> IconFileNames = new()
        {
            { DialogGraphIconId.ToolbarOpen, "icon_toolbar_open.svg" },
            { DialogGraphIconId.ToolbarSave, "icon_toolbar_save.svg" },
            { DialogGraphIconId.ToolbarValidate, "icon_toolbar_validate.svg" },
            { DialogGraphIconId.ToolbarPreview, "icon_toolbar_preview.svg" },
            { DialogGraphIconId.ToolbarSettings, "icon_toolbar_settings.svg" },
            { DialogGraphIconId.ToolbarAutoLayout, "icon_toolbar_auto_layout.svg" },

            { DialogGraphIconId.NodeStart, "icon_node_start.svg" },
            { DialogGraphIconId.NodeEnd, "icon_node_end.svg" },
            { DialogGraphIconId.NodeDialog, "icon_node_dialog.svg" },
            { DialogGraphIconId.NodeChoice, "icon_node_choice.svg" },
            { DialogGraphIconId.NodeAction, "icon_node_action.svg" },
            { DialogGraphIconId.NodeCondition, "icon_node_condition.svg" },
            { DialogGraphIconId.NodeNarration, "icon_node_narration.svg" },
            { DialogGraphIconId.NodeAiDraft, "icon_node_ai_draft.svg" },
            { DialogGraphIconId.NodeOutcome, "icon_node_outcome.svg" },

            { DialogGraphIconId.AiGenerate, "icon_ai_generate.svg" },
            { DialogGraphIconId.AiRewrite, "icon_ai_rewrite.svg" },
            { DialogGraphIconId.AiSuggestChoices, "icon_ai_suggest_choices.svg" },
            { DialogGraphIconId.AiContinueBranch, "icon_ai_continue_branch.svg" },
            { DialogGraphIconId.AiHistory, "icon_ai_history.svg" },
            { DialogGraphIconId.AiProvider, "icon_ai_provider.svg" },

            { DialogGraphIconId.StatusSuccess, "icon_status_success.svg" },
            { DialogGraphIconId.StatusWarning, "icon_status_warning.svg" },
            { DialogGraphIconId.StatusError, "icon_status_error.svg" },
            { DialogGraphIconId.StatusInfo, "icon_status_info.svg" },
            { DialogGraphIconId.StatusLocked, "icon_status_locked.svg" },
            { DialogGraphIconId.StatusConnected, "icon_status_connected.svg" },
            { DialogGraphIconId.StatusDisconnected, "icon_status_disconnected.svg" },

            { DialogGraphIconId.ActionAdd, "icon_action_add.svg" },
            { DialogGraphIconId.ActionDelete, "icon_action_delete.svg" },
            { DialogGraphIconId.ActionDuplicate, "icon_action_duplicate.svg" },
            { DialogGraphIconId.ActionEdit, "icon_action_edit.svg" },
            { DialogGraphIconId.ActionApply, "icon_action_apply.svg" },
            { DialogGraphIconId.ActionCancel, "icon_action_cancel.svg" },
            { DialogGraphIconId.ActionExpand, "icon_action_expand.svg" },
            { DialogGraphIconId.ActionCollapse, "icon_action_collapse.svg" },
            { DialogGraphIconId.ActionMore, "icon_action_more.svg" }
        };

        private sealed class DialogGraphIconAsset
        {
            public Sprite Sprite { get; set; }
            public Texture2D Texture { get; set; }

            public bool IsValid =>
                Sprite != null ||
                Texture != null;
        }

        private static readonly Dictionary<DialogGraphIconId, DialogGraphIconAsset> Cache = new();
        private static readonly HashSet<DialogGraphIconId> MissingWarnings = new();
        private static readonly Dictionary<string, Color> TintByClassName = new(StringComparer.Ordinal)
        {
            { "dgs-icon--muted", ParseColor("#7d8595") },
            { "dgs-icon--brand", ParseColor("#86b7ff") },
            { "dgs-icon--ai", ParseColor("#97e2ab") },
            { "dgs-icon--success", ParseColor("#75cf8d") },
            { "dgs-icon--warning", ParseColor("#f0c25d") },
            { "dgs-icon--error", ParseColor("#ef8a8a") },
            { "dgs-icon--dialog", ParseColor("#f2bf73") },
            { "dgs-icon--choice", ParseColor("#8fc2ff") },
            { "dgs-icon--action", ParseColor("#c0a3ff") },
            { "dgs-icon--condition", ParseColor("#78d6cf") },
            { "dgs-icon--outcome", ParseColor("#22d3ee") },
            { "dgs-icon--start", ParseColor("#7fd487") },
            { "dgs-icon--end", ParseColor("#ef7e78") }
        };
        private static readonly Color DefaultTint = ParseColor("#d4d8e0");

        private static StyleSheet _iconStyleSheet;



        public static bool HasIcon(DialogGraphIconId id)
        {
            if (Cache.TryGetValue(id, out var cached))
            {
                return cached != null && cached.IsValid;
            }

            if (!IconFileNames.TryGetValue(id, out var fileName))
            {
                WarnMissing(id, $"No dialog graph icon mapping exists for '{id}'.");
                Cache[id] = null;
                return false;
            }

            var asset = LoadAssetAtPath(fileName);
            if (asset == null || !asset.IsValid)
            {
                WarnMissing(id, $"Dialog graph icon '{id}' is missing or imported as an unsupported type for '{fileName}'.");
                Cache[id] = null;
                return false;
            }

            Cache[id] = asset;
            return true;
        }

        public static Image CreateImage(DialogGraphIconId id, params string[] classNames)
        {
            var image = new Image
            {
                name = $"dgs-icon-{id}",
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore
            };

            AttachIconStyleSheet(image);
            image.AddToClassList("dgs-icon");
            AddClassNames(image, classNames);
            SetImage(image, id);
            ApplyTint(image, image.GetClasses());
            return image;
        }

        public static VisualElement CreateBackgroundIcon(DialogGraphIconId id, params string[] classNames)
        {
            var element = new VisualElement
            {
                name = $"dgs-icon-{id}",
                pickingMode = PickingMode.Ignore
            };

            AttachIconStyleSheet(element);
            element.AddToClassList("dgs-icon");
            AddClassNames(element, classNames);
            SetBackgroundIcon(element, id);
            return element;
        }

        public static void SetImage(Image image, DialogGraphIconId id)
        {
            if (image == null)
            {
                Debug.LogWarning($"[DialogGraphIconManager] Cannot set image for '{id}' on a null Image.");
                return;
            }

            AttachIconStyleSheet(image);
            AssignToImage(image, id);
            ApplyTint(image, image.GetClasses());
        }

        public static void SetBackgroundIcon(VisualElement element, DialogGraphIconId id)
        {
            if (element == null)
            {
                Debug.LogWarning($"[DialogGraphIconManager] Cannot set background icon '{id}' on a null VisualElement.");
                return;
            }

            AttachIconStyleSheet(element);

            if (element is Image image)
            {
                AssignToImage(image, id);
                ApplyTint(image, image.GetClasses());
                return;
            }

            var iconImage = element.Q<Image>(BackgroundImageChildName);
            if (iconImage == null)
            {
                iconImage = new Image
                {
                    name = BackgroundImageChildName,
                    pickingMode = PickingMode.Ignore,
                    scaleMode = ScaleMode.ScaleToFit
                };

                iconImage.AddToClassList("dgs-icon__image");
                element.Add(iconImage);
            }

            AttachIconStyleSheet(iconImage);
            AssignToImage(iconImage, id);
            ApplyTint(iconImage, element.GetClasses());
        }

        public static void ValidateIconReferences()
        {
            var missing = new List<string>();

            foreach (DialogGraphIconId id in Enum.GetValues(typeof(DialogGraphIconId)))
            {
                if (!IconFileNames.TryGetValue(id, out var fileName))
                {
                    missing.Add($"{id}: missing mapping entry");
                    continue;
                }

                var asset = LoadAssetAtPath(fileName);
                if (asset == null || !asset.IsValid)
                {
                    missing.Add($"{id}: missing PNG/SVG asset for {fileName}");
                }
            }

            if (missing.Count == 0)
            {
                Debug.Log("[DialogGraphIconManager] All dialog graph icon references are valid.");
                return;
            }

            Debug.LogWarning("[DialogGraphIconManager] Missing dialog graph icon references:\n" + string.Join("\n", missing));
        }

        private static void AttachIconStyleSheet(VisualElement element)
        {
            if (_iconStyleSheet == null)
            {
                _iconStyleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(TextResources.ICON_STYLE_PATH);
                if (_iconStyleSheet == null)
                {
                    Debug.LogWarning($"[DialogGraphIconManager] Icon stylesheet missing at '{TextResources.ICON_STYLE_PATH}'.");
                    return;
                }
            }

            if (!element.styleSheets.Contains(_iconStyleSheet))
            {
                element.styleSheets.Add(_iconStyleSheet);
            }
        }

        private static void AddClassNames(VisualElement element, IEnumerable<string> classNames)
        {
            if (element == null || classNames == null)
            {
                return;
            }

            foreach (var className in classNames)
            {
                if (!string.IsNullOrWhiteSpace(className))
                {
                    element.AddToClassList(className);
                }
            }
        }

        private static void WarnMissing(DialogGraphIconId id, string message)
        {
            if (!MissingWarnings.Add(id))
            {
                return;
            }

            Debug.LogWarning($"[DialogGraphIconManager] {message}");
        }

        private static DialogGraphIconAsset LoadAsset(DialogGraphIconId id)
        {
            if (HasIcon(id) && Cache.TryGetValue(id, out var asset))
            {
                return asset;
            }

            return null;
        }

        private static DialogGraphIconAsset LoadAssetAtPath(string fileName)
        {
            var svgAssetPath = $"{IconFolderPath}/{fileName}";
            var pngAssetPath = $"{IconFolderPath}/{Path.ChangeExtension(fileName, ".png")}";

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(pngAssetPath);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(pngAssetPath);

            if (texture == null || sprite == null)
            {
                var pngAssets = AssetDatabase.LoadAllAssetsAtPath(pngAssetPath);
                sprite ??= pngAssets.OfType<Sprite>().FirstOrDefault();
                texture ??= pngAssets.OfType<Texture2D>().FirstOrDefault();
            }

            return new DialogGraphIconAsset
            {
                Sprite = sprite,
                Texture = texture != null || sprite != null
                    ? texture
                    : CreateFallbackTexture(fileName)
            };
        }

        private static Texture2D CreateFallbackTexture(string fileName)
        {
            const int size = 16;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = $"DGS_Fallback_{Path.GetFileNameWithoutExtension(fileName)}",
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color[size * size];
            var center = (size - 1) / 2f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x - center;
                    var dy = y - center;
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);
                    pixels[y * size + x] = distance <= 6f ? Color.white : Color.clear;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static void AssignToImage(Image image, DialogGraphIconId id)
        {
            image.scaleMode = ScaleMode.ScaleToFit;

            var asset = LoadAsset(id);
            if (asset == null)
            {
                image.vectorImage = null;
                image.image = null;
                image.style.backgroundImage = StyleKeyword.None;
                return;
            }

            if (asset.Texture != null || asset.Sprite != null)
            {
                image.vectorImage = null;
                image.image = asset.Texture != null
                    ? asset.Texture
                    : asset.Sprite.texture;
                image.style.backgroundImage = StyleKeyword.None;
                return;
            }

            image.vectorImage = null;
            image.image = null;
            image.style.backgroundImage = StyleKeyword.None;
        }

        private static void ApplyTint(Image image, IEnumerable<string> classNames)
        {
            if (image == null)
            {
                return;
            }

            var tint = ResolveTint(classNames);
            image.tintColor = tint;
            image.style.unityBackgroundImageTintColor = tint;
        }

        private static Color ResolveTint(IEnumerable<string> classNames)
        {
            if (classNames != null)
            {
                foreach (var className in classNames)
                {
                    if (className != null && TintByClassName.TryGetValue(className, out var tint))
                    {
                        return tint;
                    }
                }
            }

            return DefaultTint;
        }

        private static Color ParseColor(string htmlColor)
        {
            return ColorUtility.TryParseHtmlString(htmlColor, out var color)
                ? color
                : Color.white;
        }
    }
}