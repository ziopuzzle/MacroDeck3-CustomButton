namespace Ziopuzzle.CustomButton;

/// <summary>An interaction with an explicitly enabled XML element, not its containing tile.</summary>
public sealed record ControlInput(string ElementId, string EventName, double? Value = null, double? Level = null, string? DataKey = null);
