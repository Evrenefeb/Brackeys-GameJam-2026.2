namespace DialogSystem.Runtime.Models.Nodes
{
    /// <summary>Classification of graph node types.</summary>
    public enum NodeKind
    {
        None = 0,
        Start = 1,
        End = 2,
        Dialog = 3,
        Choice = 4,
        Action = 5,
        Condition = 6,
        VariableMutation = 7,
        GraphJump = 8,
        Outcome = 9
    }
}