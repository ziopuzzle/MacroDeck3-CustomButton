using MacroDeck.Plugin.Hosting;
using MacroDeck.Plugin.Serilog;
using Ziopuzzle.CustomButton;

var plugin = MacroDeckPlugin.CreatePlugin(args)
    .UseMacroDeckLogging()
    .UseLocalization(Strings.LocalizationCatalog)
    .RegisterIntegration<CustomButtonIntegration>()
    .Build();
await plugin.RunAsync();
