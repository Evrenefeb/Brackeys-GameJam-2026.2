using UnityEngine;

namespace DialogSystem.Runtime.Interfaces
{
    /// <summary>Base interface for any text reveal effect.</summary>
    public interface ITextRevealEffect
    {
        bool IsCancelled { get; }
        void Cancel();                     // Mark as cancelled; Play() must stop yielding quickly
        void CompleteImmediately();        // Force UI into “full text” state
        System.Collections.IEnumerator Play(); // DO NOT start coroutines inside; just yield here
    }
}
