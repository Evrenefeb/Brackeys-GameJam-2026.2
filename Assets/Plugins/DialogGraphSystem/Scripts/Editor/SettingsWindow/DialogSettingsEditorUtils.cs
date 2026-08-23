using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using DialogSystem.EditorTools.Resources;

namespace DialogSystem.EditorTools.Settings
{
    /// <summary>
    /// Helper builders for consistent UI Toolkit rows (slider+pill, toggles, cards, icons).
    /// </summary>
    internal static class DialogSettingsEditorUtils
    {
        #region ---------------- Slider + Pill (float) ----------------
        public static VisualElement SliderWithPill(SerializedObject so, string floatProp, float min, float max, string labelText)
        {
            var row = new VisualElement(); row.AddToClassList("dgs-row");

            var label = new Label(labelText); label.AddToClassList("dgs-muted");
            row.Add(label);

            var slider = new Slider(min, max) { showInputField = false };
            slider.AddToClassList("dgs-slider");

            var prop = so.FindProperty(floatProp);
            slider.value = prop.floatValue;

            slider.RegisterValueChangedCallback(evt =>
            {
                prop.floatValue = evt.newValue;
                so.ApplyModifiedProperties();
            });

            var pill = new FloatField() { value = prop.floatValue };
            pill.AddToClassList("dgs-pill");
            pill.RegisterValueChangedCallback(evt =>
            {
                prop.floatValue = Mathf.Clamp(evt.newValue, min, max);
                so.ApplyModifiedProperties();
                slider.SetValueWithoutNotify(prop.floatValue);
            });

            // Passive keep-in-sync; light frequency to avoid perf cost in editor.
            row.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                slider.schedule.Execute(() =>
                {
                    prop.serializedObject.Update();
                    slider.SetValueWithoutNotify(prop.floatValue);
                    pill.SetValueWithoutNotify(prop.floatValue);
                }).Every(150);
            });

            row.Add(slider);
            row.Add(pill);
            return row;
        }
        #endregion

        #region ---------------- Slider + Pill (int) ----------------
        public static VisualElement IntSliderWithPill(SerializedObject so, string intProp, int min, int max, string labelText)
        {
            var row = new VisualElement(); row.AddToClassList("dgs-row");

            var label = new Label(labelText); label.AddToClassList("dgs-muted");
            row.Add(label);

            var slider = new SliderInt(min, max);
            slider.AddToClassList("dgs-slider");

            var prop = so.FindProperty(intProp);
            slider.value = prop.intValue;

            slider.RegisterValueChangedCallback(evt =>
            {
                prop.intValue = evt.newValue;
                so.ApplyModifiedProperties();
            });

            var pill = new IntegerField() { value = prop.intValue };
            pill.AddToClassList("dgs-pill");
            pill.RegisterValueChangedCallback(evt =>
            {
                prop.intValue = Mathf.Clamp(evt.newValue, min, max);
                so.ApplyModifiedProperties();
                slider.SetValueWithoutNotify(prop.intValue);
            });

            row.Add(slider);
            row.Add(pill);
            return row;
        }
        #endregion

        #region ---------------- Toggle Row ----------------
        public static Toggle ToggleRow(SerializedObject so, string boolProp, string labelText)
        {
            var t = new Toggle(labelText);
            t.AddToClassList("dgs-row");
            var p = so.FindProperty(boolProp);
            t.value = p.boolValue;
            t.RegisterValueChangedCallback(evt =>
            {
                p.boolValue = evt.newValue;
                so.ApplyModifiedProperties();
            });
            return t;
        }
        #endregion

        #region ---------------- Settings Property Fields ----------------
        public static PropertyField SettingsPropertyField(
            SerializedObject so,
            string propertyName,
            string labelText,
            bool isExpandable = false)
        {
            var field = new PropertyField(so.FindProperty(propertyName), labelText);
            field.AddToClassList("dgs-property-field");

            if (!isExpandable)
            {
                return field;
            }

            field.AddToClassList("dgs-property-field--expandable");
            field.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                field.schedule.Execute(() =>
                {
                    var foldout = field.Q<Foldout>();
                    if (foldout != null)
                    {
                        foldout.style.width = Length.Percent(100);
                        foldout.style.flexGrow = 0;
                        foldout.style.flexShrink = 0;
                    }

                    var content = field.Q(className: "unity-foldout__content");
                    if (content != null)
                    {
                        content.style.overflow = Overflow.Visible;
                        content.style.flexGrow = 0;
                        content.style.flexShrink = 0;
                    }
                }).StartingIn(0);
            });

            return field;
        }
        #endregion

        #region ---------------- Key Capture Row ----------------
        public static VisualElement KeyCodeCaptureRow(
            SerializedObject so,
            string propertyName,
            string labelText,
            string hintText = "")
        {
            var prop = so.FindProperty(propertyName);
            return KeyCaptureRow(
                labelText,
                () => prop != null ? (KeyCode)prop.intValue : KeyCode.None,
                key =>
                {
                    if (prop == null)
                    {
                        return;
                    }

                    prop.intValue = (int)key;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(so.targetObject);
                },
                hintText);
        }

        public static VisualElement StringKeyCaptureRow(
            SerializedObject so,
            string propertyName,
            string labelText,
            KeyCode fallbackKey,
            string hintText = "")
        {
            var prop = so.FindProperty(propertyName);
            return KeyCaptureRow(
                labelText,
                () =>
                {
                    if (prop == null || !TryParseKeyCode(prop.stringValue, out var key))
                    {
                        return fallbackKey;
                    }

                    return key;
                },
                key =>
                {
                    if (prop == null)
                    {
                        return;
                    }

                    prop.stringValue = key.ToString();
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(so.targetObject);
                },
                hintText);
        }

        private static VisualElement KeyCaptureRow(
            string labelText,
            System.Func<KeyCode> getValue,
            System.Action<KeyCode> setValue,
            string hintText)
        {
            var root = new VisualElement();
            root.AddToClassList("dgs-keybind");
            root.style.width = Length.Percent(100);
            root.style.marginTop = 8;
            root.style.marginBottom = 8;
            root.style.flexShrink = 0;

            var row = new VisualElement();
            row.AddToClassList("dgs-keybind-row");
            row.style.minHeight = 42;
            row.style.paddingLeft = 10;
            row.style.paddingRight = 10;
            row.style.paddingTop = 8;
            row.style.paddingBottom = 8;
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.backgroundColor = new Color(0.075f, 0.083f, 0.118f, 1f);
            row.style.borderTopWidth = 1;
            row.style.borderRightWidth = 1;
            row.style.borderBottomWidth = 1;
            row.style.borderLeftWidth = 1;
            row.style.borderTopColor = new Color(0.165f, 0.184f, 0.271f, 1f);
            row.style.borderRightColor = new Color(0.165f, 0.184f, 0.271f, 1f);
            row.style.borderBottomColor = new Color(0.165f, 0.184f, 0.271f, 1f);
            row.style.borderLeftColor = new Color(0.165f, 0.184f, 0.271f, 1f);
            row.style.borderTopLeftRadius = 6;
            row.style.borderTopRightRadius = 6;
            row.style.borderBottomLeftRadius = 6;
            row.style.borderBottomRightRadius = 6;

            var labelColumn = new VisualElement();
            labelColumn.AddToClassList("dgs-keybind-label-column");
            labelColumn.style.flexDirection = FlexDirection.Column;
            labelColumn.style.flexGrow = 1;
            labelColumn.style.flexShrink = 1;
            labelColumn.style.minWidth = 0;
            labelColumn.style.paddingRight = 12;

            var label = new Label(labelText);
            label.AddToClassList("dgs-keybind-label");
            label.style.color = new Color(0.91f, 0.925f, 0.957f, 1f);
            label.style.fontSize = 11;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            labelColumn.Add(label);

            if (!string.IsNullOrWhiteSpace(hintText))
            {
                var hint = new Label(hintText);
                hint.AddToClassList("dgs-keybind-hint");
                hint.style.color = new Color(0.36f, 0.392f, 0.502f, 1f);
                hint.style.fontSize = 10;
                hint.style.marginTop = 2;
                hint.style.whiteSpace = WhiteSpace.Normal;
                labelColumn.Add(hint);
            }

            var captureButton = new Button();
            captureButton.AddToClassList("dgs-keybind-button");
            captureButton.focusable = true;
            captureButton.style.minWidth = 136;
            captureButton.style.maxWidth = 180;
            captureButton.style.height = 28;
            captureButton.style.paddingLeft = 14;
            captureButton.style.paddingRight = 14;
            captureButton.style.borderTopLeftRadius = 5;
            captureButton.style.borderTopRightRadius = 5;
            captureButton.style.borderBottomLeftRadius = 5;
            captureButton.style.borderBottomRightRadius = 5;
            captureButton.style.borderTopWidth = 1;
            captureButton.style.borderRightWidth = 1;
            captureButton.style.borderBottomWidth = 1;
            captureButton.style.borderLeftWidth = 1;
            captureButton.style.fontSize = 11;
            captureButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            captureButton.style.unityTextAlign = TextAnchor.MiddleCenter;
            captureButton.style.flexShrink = 0;
            captureButton.style.whiteSpace = WhiteSpace.NoWrap;

            var isCapturing = false;
            var ignoreNextClick = false;

            void Refresh()
            {
                captureButton.text = isCapturing
                    ? "Press a key..."
                    : FormatKeyCodeForBinding(getValue());
                captureButton.EnableInClassList("is-recording", isCapturing);
                captureButton.style.backgroundColor = isCapturing
                    ? new Color(0.16f, 0.18f, 0.35f, 1f)
                    : new Color(0.05f, 0.06f, 0.086f, 1f);
                captureButton.style.color = Color.white;
                var borderColor = isCapturing
                    ? new Color(0.655f, 0.545f, 0.98f, 1f)
                    : new Color(0.24f, 0.267f, 0.376f, 1f);
                captureButton.style.borderTopColor = borderColor;
                captureButton.style.borderRightColor = borderColor;
                captureButton.style.borderBottomColor = borderColor;
                captureButton.style.borderLeftColor = borderColor;
            }

            void Apply(KeyCode key)
            {
                if (key == KeyCode.None)
                {
                    return;
                }

                isCapturing = false;
                setValue?.Invoke(key);
                Refresh();
            }

            captureButton.clicked += () =>
            {
                if (ignoreNextClick)
                {
                    ignoreNextClick = false;
                    return;
                }

                isCapturing = true;
                Refresh();
                captureButton.Focus();
            };

            captureButton.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (!isCapturing)
                {
                    return;
                }

                if (evt.keyCode == KeyCode.Escape)
                {
                    isCapturing = false;
                    Refresh();
                    evt.StopImmediatePropagation();
                    return;
                }

                Apply(evt.keyCode);
                evt.StopImmediatePropagation();
            }, TrickleDown.TrickleDown);

            captureButton.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (!isCapturing)
                {
                    return;
                }

                Apply(MouseButtonToKeyCode(evt.button));
                ignoreNextClick = true;
                evt.StopImmediatePropagation();
            }, TrickleDown.TrickleDown);

            captureButton.RegisterCallback<FocusOutEvent>(_ =>
            {
                if (!isCapturing)
                {
                    return;
                }

                isCapturing = false;
                Refresh();
            });

            captureButton.RegisterCallback<AttachToPanelEvent>(_ => Refresh());

            row.Add(labelColumn);
            row.Add(captureButton);
            root.Add(row);
            return root;
        }

        private static bool TryParseKeyCode(string rawValue, out KeyCode key)
        {
            key = KeyCode.None;

            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return false;
            }

            var value = rawValue.Trim();
            if (System.Enum.TryParse(value, true, out key) && key != KeyCode.None)
            {
                return true;
            }

            if (value.Length == 1)
            {
                var c = char.ToUpperInvariant(value[0]);
                if (c >= 'A' && c <= 'Z')
                {
                    key = (KeyCode)((int)KeyCode.A + (c - 'A'));
                    return true;
                }

                if (c >= '0' && c <= '9')
                {
                    key = (KeyCode)((int)KeyCode.Alpha0 + (c - '0'));
                    return true;
                }
            }

            key = value.ToUpperInvariant() switch
            {
                "ENTER" => KeyCode.Return,
                "ESC" => KeyCode.Escape,
                "CTRL" => KeyCode.LeftControl,
                "CONTROL" => KeyCode.LeftControl,
                "SHIFT" => KeyCode.LeftShift,
                "ALT" => KeyCode.LeftAlt,
                _ => KeyCode.None
            };

            return key != KeyCode.None;
        }

        private static KeyCode MouseButtonToKeyCode(int button)
        {
            return button switch
            {
                0 => KeyCode.Mouse0,
                1 => KeyCode.Mouse1,
                2 => KeyCode.Mouse2,
                3 => KeyCode.Mouse3,
                4 => KeyCode.Mouse4,
                5 => KeyCode.Mouse5,
                6 => KeyCode.Mouse6,
                _ => KeyCode.None
            };
        }

        private static string FormatKeyCodeForBinding(KeyCode key)
        {
            if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9)
            {
                return ((int)key - (int)KeyCode.Alpha0).ToString();
            }

            if (key >= KeyCode.Keypad0 && key <= KeyCode.Keypad9)
            {
                return "Keypad " + ((int)key - (int)KeyCode.Keypad0);
            }

            if (key >= KeyCode.Mouse0 && key <= KeyCode.Mouse6)
            {
                return "Mouse " + ((int)key - (int)KeyCode.Mouse0 + 1);
            }

            if (key >= KeyCode.F1 && key <= KeyCode.F15)
            {
                return key.ToString();
            }

            return key switch
            {
                KeyCode.None => "Unbound",
                KeyCode.Space => "Space",
                KeyCode.Return => "Enter",
                KeyCode.KeypadEnter => "Keypad Enter",
                KeyCode.Escape => "Esc",
                KeyCode.UpArrow => "Up Arrow",
                KeyCode.DownArrow => "Down Arrow",
                KeyCode.LeftArrow => "Left Arrow",
                KeyCode.RightArrow => "Right Arrow",
                KeyCode.LeftControl => "Left Ctrl",
                KeyCode.RightControl => "Right Ctrl",
                KeyCode.LeftShift => "Left Shift",
                KeyCode.RightShift => "Right Shift",
                KeyCode.LeftAlt => "Left Alt",
                KeyCode.RightAlt => "Right Alt",
                _ => key.ToString()
            };
        }
        #endregion

        #region ---------------- Card & Icon ----------------
        public static VisualElement Card(string title)
        {
            var card = new VisualElement(); card.AddToClassList("dgs-card");
            var h = new Label(title); h.AddToClassList("dgs-card-title");
            card.Add(h);
            return card;
        }

        public static VisualElement Icon(DialogGraphIconId iconId, params string[] classNames)
        {
            var icon = DialogGraphIconManager.CreateBackgroundIcon(iconId, "dgs-nav-icon");
            if (classNames == null)
            {
                return icon;
            }

            foreach (var className in classNames)
            {
                if (!string.IsNullOrWhiteSpace(className))
                {
                    icon.AddToClassList(className);
                }
            }

            return icon;
        }
        #endregion

        #region ---------------- Advanced Slider (with labels + live value + recommended range) ----------------
        public static VisualElement AdvancedSliderWithValue(
         SerializedObject so,
         string propertyName,
         float min,
         float max,
         string labelText,
         Vector2 recommendedRange)
        {
            var root = new VisualElement();
            root.AddToClassList("dgs-adv-row");

            var prop = so.FindProperty(propertyName);

            // ---- Header with value on the same line ----
            var header = new VisualElement();
            header.AddToClassList("dgs-adv-header");

            var label = new Label(labelText);
            label.AddToClassList("dgs-adv-label");
            header.Add(label);

            // Value badge on the right side of header
            var valueBadge = new Label(prop.floatValue.ToString("0.##"));
            valueBadge.AddToClassList("dgs-adv-value-badge");
            valueBadge.style.backgroundColor = new Color(0.18f, 0.20f, 0.24f, 1f);
            valueBadge.style.color = new Color(0.82f, 0.87f, 0.95f, 1f);
            valueBadge.style.paddingLeft = 8;
            valueBadge.style.paddingRight = 8;
            valueBadge.style.paddingTop = 2;
            valueBadge.style.paddingBottom = 2;
            valueBadge.style.borderTopLeftRadius = 4;
            valueBadge.style.borderTopRightRadius = 4;
            valueBadge.style.borderBottomLeftRadius = 4;
            valueBadge.style.borderBottomRightRadius = 4;
            valueBadge.style.fontSize = 11;
            valueBadge.style.unityFontStyleAndWeight = FontStyle.Bold;
            valueBadge.style.minWidth = 40;
            valueBadge.style.unityTextAlign = TextAnchor.MiddleCenter;

            header.Add(valueBadge);
            root.Add(header);

            // ---- Slider Container with Floating Value ----
            var sliderRow = new VisualElement();
            sliderRow.AddToClassList("dgs-adv-slider-container");
            sliderRow.style.position = Position.Relative;

            // --- Track background container ---
            var trackContainer = new VisualElement
            {
                style =
            {
                position = Position.Relative,
                flexGrow = 1,
                marginTop = 2,
                marginBottom = 2,
                minHeight = 24
            }
            };

            // Recommended range overlay (centered vertically)
            var recommendedOverlay = new VisualElement
            {
                style =
            {
                position = Position.Absolute,
                top = new StyleLength(new Length(25, LengthUnit.Percent)),
                height = 10,
                backgroundColor = new Color(0.25f, 0.85f, 0.55f, 0.25f),
                borderTopLeftRadius = 3,
                borderBottomLeftRadius = 3,
                borderTopRightRadius = 3,
                borderBottomRightRadius = 3,
                unityBackgroundImageTintColor = new Color(1, 1, 1, 0.3f)
            }
            };

            // Slider
            var slider = new Slider(min, max) { value = prop.floatValue };
            slider.AddToClassList("dgs-adv-slider");

            // Floating value tooltip (follows the handle)
            var floatingValue = new Label(prop.floatValue.ToString("0.##"));
            floatingValue.style.position = Position.Absolute;
            floatingValue.style.top = -24;
            floatingValue.style.backgroundColor = new Color(0.2f, 0.42f, 0.84f, 0.95f);
            floatingValue.style.color = Color.white;
            floatingValue.style.paddingLeft = 6;
            floatingValue.style.paddingRight = 6;
            floatingValue.style.paddingTop = 3;
            floatingValue.style.paddingBottom = 3;
            floatingValue.style.borderTopLeftRadius = 4;
            floatingValue.style.borderTopRightRadius = 4;
            floatingValue.style.borderBottomLeftRadius = 4;
            floatingValue.style.borderBottomRightRadius = 4;
            floatingValue.style.fontSize = 10;
            floatingValue.style.unityFontStyleAndWeight = FontStyle.Bold;
            floatingValue.style.minWidth = 35;
            floatingValue.style.unityTextAlign = TextAnchor.MiddleCenter;
            floatingValue.style.display = DisplayStyle.None; // Hidden by default

            // Add elements in order
            trackContainer.Add(recommendedOverlay);
            trackContainer.Add(slider);
            trackContainer.Add(floatingValue);

            sliderRow.Add(trackContainer);
            root.Add(sliderRow);

            // ---- Recommended range text ----
            var rec = new Label($"recommended: {recommendedRange.x:0.#}–{recommendedRange.y:0.#}");
            rec.AddToClassList("dgs-adv-range-label");
            root.Add(rec);

            // ---- Logic to position the green overlay ----
            void UpdateOverlayPosition()
            {
                float rangeMinRatio = Mathf.InverseLerp(min, max, recommendedRange.x);
                float rangeMaxRatio = Mathf.InverseLerp(min, max, recommendedRange.y);

                float leftPercent = Mathf.Clamp01(rangeMinRatio) * 100f;
                float widthPercent = Mathf.Clamp01(rangeMaxRatio - rangeMinRatio) * 100f;

                recommendedOverlay.style.left = new Length(leftPercent, LengthUnit.Percent);
                recommendedOverlay.style.width = new Length(widthPercent, LengthUnit.Percent);
            }

            UpdateOverlayPosition();

            // ---- Update floating value position ----
            void UpdateFloatingValuePosition(float value)
            {
                float ratio = Mathf.InverseLerp(min, max, value);
                float leftPercent = Mathf.Clamp01(ratio) * 100f;
                floatingValue.style.left = new Length(leftPercent, LengthUnit.Percent);
            }

            UpdateFloatingValuePosition(prop.floatValue);

            // ---- Event handlers ----
            slider.RegisterValueChangedCallback(evt =>
            {
                prop.floatValue = evt.newValue;
                so.ApplyModifiedProperties();

                string valueText = evt.newValue.ToString("0.##");
                valueBadge.text = valueText;
                floatingValue.text = valueText;

                UpdateFloatingValuePosition(evt.newValue);
            });

            // Show floating value on hover/focus
            slider.RegisterCallback<MouseEnterEvent>(evt =>
            {
                floatingValue.style.display = DisplayStyle.Flex;
            });

            slider.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                floatingValue.style.display = DisplayStyle.None;
            });

            slider.RegisterCallback<FocusInEvent>(evt =>
            {
                floatingValue.style.display = DisplayStyle.Flex;
            });

            slider.RegisterCallback<FocusOutEvent>(evt =>
            {
                floatingValue.style.display = DisplayStyle.None;
            });

            return root;
        }

        #endregion

        #region ---------------- Advanced Enum Dropdown ----------------
        public static VisualElement AdvancedEnumDropdown(
            SerializedObject so,
            string propertyName,
            string labelText,
            string hintText = "",
            bool isLocked = false)
        {
            var root = new VisualElement();
            root.AddToClassList("dgs-adv-dropdown");

            var prop = so.FindProperty(propertyName);

            // Header label
            var header = new VisualElement();
            header.AddToClassList("dgs-adv-dropdown-row");
            var label = new Label(labelText);
            label.AddToClassList("dgs-adv-dropdown-label");
            header.Add(label);
            root.Add(header);

            // Dropdown itself
            var enumField = new PropertyField(prop, "");
            enumField.AddToClassList("dgs-adv-enumfield");
            root.Add(enumField);

            // Disable interaction if locked
            if (isLocked)
                enumField.SetEnabled(false);

            // Optional hint
            if (!string.IsNullOrEmpty(hintText))
            {
                var hint = new Label(hintText);
                hint.AddToClassList("dgs-adv-hint");
                root.Add(hint);
            }

            return root;
        }
        #endregion
    }
}