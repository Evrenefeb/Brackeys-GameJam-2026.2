using System;

namespace DialogSystem.Runtime.Core
{
    /// <summary>
    /// Keeps dialogue runtime state transitions explicit and inspectable.
    /// </summary>
    internal sealed class DialogueRuntimeStateMachine
    {
        public DialogueRuntimeState State { get; private set; } = DialogueRuntimeState.Idle;

        public void Reset()
        {
            State = DialogueRuntimeState.Idle;
        }

        public bool TryTransition(DialogueRuntimeState nextState, out string error)
        {
            error = string.Empty;

            if (State == nextState)
            {
                return true;
            }

            if (!IsValidTransition(State, nextState))
            {
                error = $"Invalid dialogue runtime transition: {State} -> {nextState}.";
                return false;
            }

            State = nextState;
            return true;
        }

        private static bool IsValidTransition(DialogueRuntimeState current, DialogueRuntimeState next)
        {
            if (next == DialogueRuntimeState.Failed || next == DialogueRuntimeState.Idle)
            {
                return true;
            }

            switch (current)
            {
                case DialogueRuntimeState.Idle:
                    return next == DialogueRuntimeState.Starting;

                case DialogueRuntimeState.Starting:
                    return next == DialogueRuntimeState.PresentingLine ||
                           next == DialogueRuntimeState.Typing ||
                           next == DialogueRuntimeState.PresentingChoices ||
                           next == DialogueRuntimeState.ExecutingAction ||
                           next == DialogueRuntimeState.EvaluatingCondition ||
                           next == DialogueRuntimeState.Waiting ||
                           next == DialogueRuntimeState.Completed;

                case DialogueRuntimeState.PresentingLine:
                    return next == DialogueRuntimeState.Typing ||
                           next == DialogueRuntimeState.WaitingForContinue ||
                           next == DialogueRuntimeState.ExecutingAction ||
                           next == DialogueRuntimeState.EvaluatingCondition ||
                           next == DialogueRuntimeState.Waiting ||
                           next == DialogueRuntimeState.Completed;

                case DialogueRuntimeState.Typing:
                    return next == DialogueRuntimeState.WaitingForContinue ||
                           next == DialogueRuntimeState.PresentingChoices ||
                           next == DialogueRuntimeState.ExecutingAction ||
                           next == DialogueRuntimeState.EvaluatingCondition ||
                           next == DialogueRuntimeState.Waiting ||
                           next == DialogueRuntimeState.Completed;

                case DialogueRuntimeState.WaitingForContinue:
                    return next == DialogueRuntimeState.PresentingLine ||
                           next == DialogueRuntimeState.Typing ||
                           next == DialogueRuntimeState.PresentingChoices ||
                           next == DialogueRuntimeState.ExecutingAction ||
                           next == DialogueRuntimeState.EvaluatingCondition ||
                           next == DialogueRuntimeState.Waiting ||
                           next == DialogueRuntimeState.Completed;

                case DialogueRuntimeState.PresentingChoices:
                    return next == DialogueRuntimeState.PresentingLine ||
                           next == DialogueRuntimeState.Typing ||
                           next == DialogueRuntimeState.ExecutingAction ||
                           next == DialogueRuntimeState.EvaluatingCondition ||
                           next == DialogueRuntimeState.Waiting ||
                           next == DialogueRuntimeState.Completed;

                case DialogueRuntimeState.ExecutingAction:
                case DialogueRuntimeState.EvaluatingCondition:
                case DialogueRuntimeState.Waiting:
                    return next == DialogueRuntimeState.PresentingLine ||
                           next == DialogueRuntimeState.Typing ||
                           next == DialogueRuntimeState.PresentingChoices ||
                           next == DialogueRuntimeState.ExecutingAction ||
                           next == DialogueRuntimeState.EvaluatingCondition ||
                           next == DialogueRuntimeState.Waiting ||
                           next == DialogueRuntimeState.WaitingForContinue ||
                           next == DialogueRuntimeState.Completed;

                case DialogueRuntimeState.Completed:
                case DialogueRuntimeState.Failed:
                    return next == DialogueRuntimeState.Idle ||
                           next == DialogueRuntimeState.Starting;

                default:
                    throw new ArgumentOutOfRangeException(nameof(current), current, null);
            }
        }
    }
}
