namespace DialogSystem.Runtime.Core
{
    /// <summary>
    /// High-level runtime execution state for the active dialogue session.
    /// </summary>
    public enum DialogueRuntimeState
    {
        Idle,
        Starting,
        PresentingLine,
        Typing,
        WaitingForContinue,
        PresentingChoices,
        ExecutingAction,
        EvaluatingCondition,
        Waiting,
        Completed,
        Failed
    }
}
