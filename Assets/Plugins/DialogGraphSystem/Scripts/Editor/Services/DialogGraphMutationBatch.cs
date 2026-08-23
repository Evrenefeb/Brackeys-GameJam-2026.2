using System;
using UnityEditor;

namespace DialogSystem.EditorTools.Services
{
    /// <summary>
    /// Coalesces editor asset saves for one graph mutation operation.
    /// </summary>
    public sealed class DialogGraphMutationBatch : IDisposable
    {
        private bool _saveRequested;

        public DialogGraphMutationBatch(string undoLabel)
        {
            _saveRequested = false;
        }

        public void RequestSave()
        {
            _saveRequested = true;
        }

        public void Dispose()
        {
            if (_saveRequested)
            {
                AssetDatabase.SaveAssets();
            }
        }
    }
}
