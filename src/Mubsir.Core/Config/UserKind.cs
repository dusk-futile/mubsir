namespace Mubsir.Core.Config;

/// <summary>Who this copy of Mubsir is set up for. It decides how much it says and what it may do.</summary>
public enum UserKind
{
    Unknown,
    Blind,
    LowVision,
    HandsFree,
    Beginner,
    Learner,
    ChildLearner,
}
