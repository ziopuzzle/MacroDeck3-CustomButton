using System.Text.Json;
using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.ConfigFlow;

namespace Ziopuzzle.CustomButton;

internal sealed class SharedValuesFlow : IConfigFlow
{
    internal const string Key = "sharedValues";
    private const string Title = "Shared display values";

    private static ConfigFlowStep Step(string json) => new()
    {
        StepId = Key, Title = Title,
        Description = "Set defaults for every Custom Button widget and modal. Widget initial data and channel values override matching names.",
        Fields = [new ActionParameter {
            Name = Key, Label = "Shared values (JSON)", Type = ActionParameterType.Code, Language = "json", Required = true, LiteralOnly = true,
            Description = """For example, {"themeAccent":"#54dfcc"} supplies {{themeAccent}} in your layouts. Save {} to clear all shared values.""",
            DefaultValue = json
        }]
    };

    public Task<ConfigFlowResult> StartAsync(IConfigFlowContext context, CancellationToken cancellationToken)
        => Task.FromResult(ConfigFlowResult.Step(Step("{}")));

    public Task<ConfigFlowResult> SubmitAsync(string stepId, IReadOnlyDictionary<string, object?> input,
        IConfigFlowContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var json = input.GetValueOrDefault(Key) as string ?? "";
        if (stepId != Key) return Task.FromResult(ConfigFlowResult.Error(Step(json), "Unknown settings step."));
        try { _ = DataHub.ParseValues(json); }
        catch (Exception e) when (e is JsonException or FormatException)
        { return Task.FromResult(ConfigFlowResult.Error(Step(json), fieldErrors: new Dictionary<string, MacroDeck.Localization.LocalizedText> { [Key] = e.Message })); }
        return Task.FromResult(ConfigFlowResult.Complete(Title));
    }
}
