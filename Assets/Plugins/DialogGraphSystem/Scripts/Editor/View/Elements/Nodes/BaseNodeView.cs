using System;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using DialogSystem.EditorTools.View;

namespace DialogSystem.EditorTools.View.Elements.Nodes
{
    /// <summary>
    /// Base view for node editors. Holds a reference to the ScriptableObject data and
    /// raises <see cref="OnChanged"/> when position or fields change.
    /// </summary>
    public abstract class BaseNodeView<TData> : Node where TData : ScriptableObject
    {
        public TData data { get; private set; }

        /// <summary>Fired when this view changes (e.g., moved or fields edited).</summary>
        public Action<BaseNodeView<TData>> OnChanged;

        #region Init
        public virtual void Initialize(TData data, Vector2 position, string titleText)
        {
            this.data = data;
            title = titleText;
            SetPosition(new Rect(position, new Vector2(280, 240)));

            titleButtonContainer.Clear();
            NodeUssLoader.ApplyTo(this);
        }
        #endregion

        #region Utilities
        protected void MarkDirty(UnityEngine.Object obj = null)
        {
            var target = obj != null ? obj : (UnityEngine.Object)data;
            if (target != null) EditorUtility.SetDirty(target);
            OnChanged?.Invoke(this);
        }

        public override void SetPosition(Rect newPos)
        {
            base.SetPosition(newPos);
            OnChanged?.Invoke(this);
        }
        #endregion

        #region Ports
        public abstract void RebuildPorts();
        #endregion
    }
}