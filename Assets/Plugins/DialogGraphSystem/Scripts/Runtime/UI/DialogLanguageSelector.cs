using UnityEngine;

namespace DialogSystem.Runtime.UI
{
    /// <summary>
    /// Backward-compatible wrapper. Use <see cref="DialogRuntimeLanguageController"/> for new setups.
    /// </summary>
    [DisallowMultipleComponent]
    public class DialogLanguageSelector : DialogRuntimeLanguageController
    {
    }
}
