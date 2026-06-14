namespace DoraNode;

/// <summary>
/// Represents the type of a Dora event.
/// </summary>
public enum EventType
{
    /// <summary>
    /// Signals that the Dora runtime requested the node to stop.
    /// </summary>
    Stop = 0,

    /// <summary>
    /// Carries a normal input payload for the node.
    /// </summary>
    Input = 1,

    /// <summary>
    /// Signals that an upstream input channel was closed.
    /// </summary>
    InputClosed = 2,

    /// <summary>
    /// Carries an error reported by the Dora runtime.
    /// </summary>
    Error = 3,

    /// <summary>
    /// Represents an event type that is not recognized by this SDK version.
    /// </summary>
    Unknown = 4,
}
