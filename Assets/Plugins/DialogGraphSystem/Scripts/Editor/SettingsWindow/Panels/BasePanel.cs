using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using DialogSystem.EditorTools.Resources;

namespace DialogSystem.EditorTools.Settings.Panels
{
    /// <summary>
    /// Base class for content panels with built-in scroll support.
    /// </summary>
    public abstract class BasePanel : VisualElement
    {
        #region ---------------- Fields ----------------
        protected ScrollView _scrollView;
        #endregion

        #region ---------------- Init ----------------
        protected BasePanel()
        {
            AddToClassList("dgs-content");

            // Setup the panel layout
            style.flexGrow = 1;
            style.flexDirection = FlexDirection.Column;

            // Create ScrollView for content
            _scrollView = new ScrollView(ScrollViewMode.Vertical);
            _scrollView.AddToClassList("dgs-content-scroll");
            _scrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _scrollView.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;
            _scrollView.style.flexGrow = 1;

            // Add scrollView to the panel
            base.Add(_scrollView);
        }
        #endregion

        #region ---------------- API ----------------
        public abstract void BuildUI(SerializedObject masterSo);
        #endregion

        #region ---------------- Override Add Methods ----------------
        /// <summary>
        /// Override Add to automatically add to ScrollView content instead of root.
        /// </summary>
        public new void Add(VisualElement element)
        {
            // If it's a footer, add it outside the scroll view
            if (element.ClassListContains("dgs-footer"))
            {
                base.Add(element);
            }
            else
            {
                // Everything else goes inside the scroll view
                _scrollView.Add(element);
            }
        }

        /// <summary>
        /// Method to explicitly add to root (outside scroll view).
        /// </summary>
        protected void AddToRoot(VisualElement element)
        {
            base.Add(element);
        }
        #endregion

        #region ---------------- Helpers ----------------

        /// <summary>
        /// Inserts a title + subtitle block at the very top of the scroll area.
        /// Call this at the start of BuildUI in each panel.
        /// </summary>
        protected void SetPageHeader(string title, string subtitle = "", DialogGraphIconId? iconId = null)
        {
            var header = new VisualElement();
            header.AddToClassList("dgs-page-header");

            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            
            if (iconId.HasValue)
            {
                var icon = DialogSettingsEditorUtils.Icon(iconId.Value, "dgs-nav-icon");
                icon.style.width = 24;
                icon.style.height = 24;
                icon.style.marginRight = 10;
                row.Add(icon);
            }

            var titleLabel = new Label(title);
            titleLabel.AddToClassList("dgs-page-title");
            row.Add(titleLabel);
            header.Add(row);

            if (!string.IsNullOrEmpty(subtitle))
            {
                var sub = new Label(subtitle);
                sub.AddToClassList("dgs-page-subtitle");
                header.Add(sub);
            }

            _scrollView.Insert(0, header);
        }

        /// <summary>
        /// Footer save button with dirty-state feedback.
        /// Pass a isDirty getter so the button label reflects current state.
        /// </summary>
        protected VisualElement FooterSaveWithDirty(System.Func<bool> isDirty, System.Action onSave)
        {
            var footer = new VisualElement();
            footer.AddToClassList("dgs-footer");

            Button save = null;

            void Refresh()
            {
                if (save == null) return;
                bool dirty = isDirty();
                save.text = dirty ? "SAVE" : "Saved ✓";
                if (dirty)
                {
                    save.RemoveFromClassList("dgs-save--saved");
                    save.AddToClassList("dgs-save");
                }
                else
                {
                    save.RemoveFromClassList("dgs-save");
                    save.AddToClassList("dgs-save--saved");
                }
            }

            save = new Button(() =>
            {
                onSave?.Invoke();
                // Brief delay so Unity's dirty flag clears before we re-read it
                save.schedule.Execute(Refresh).StartingIn(80);
            });
            save.AddToClassList("dgs-save");
            save.text = "SAVE";

            // Poll dirty state every 500 ms while the footer is attached
            footer.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                footer.schedule.Execute(Refresh).Every(500);
            });

            footer.Add(save);
            return footer;
        }

        /// <summary>Legacy single-action footer (kept for backward compat).</summary>
        protected VisualElement FooterSave(System.Action onClick)
        {
            var footer = new VisualElement();
            footer.AddToClassList("dgs-footer");

            var save = new Button(() => onClick?.Invoke()) { text = "SAVE" };
            save.AddToClassList("dgs-save");

            footer.Add(save);
            return footer;
        }
        #endregion
    }
}