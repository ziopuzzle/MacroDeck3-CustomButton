namespace Ziopuzzle.CustomButton;

/// <summary>An interaction with an explicitly enabled XML element, not its containing tile.</summary>
public sealed record ControlInput(string ElementId, string EventName, double? Value = null, double? Level = null, string? DataKey = null, bool? BooleanValue = null, TrackpadValue? Position = null);

public sealed record TrackpadValue(double X, double Y, double LevelX, double LevelY, string KeyX, string KeyY, double StartX, double StartY, double PreviousX, double PreviousY);
