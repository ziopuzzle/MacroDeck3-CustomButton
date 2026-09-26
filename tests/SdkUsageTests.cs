using System.Reflection;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class SdkUsageTests
{
    [Test]
    public void ReportsCompleteDeprecatedApiUsageInsteadOfVersionInference()
    {
        var usage = typeof(CustomButtonIntegration).Assembly.GetCustomAttributesData()
            .Single(a => a.AttributeType.FullName == "MacroDeck.Sdk.Deprecation.MacroDeckSdkUsageAttribute");
        Assert.That(usage.ConstructorArguments[0].Value, Is.EqualTo("3.0.0.0"));
        var deprecatedApis = (IReadOnlyCollection<CustomAttributeTypedArgument>)usage.ConstructorArguments[1].Value!;
        Assert.That(deprecatedApis, Is.Empty);
        Assert.That(usage.NamedArguments.Single(a => a.MemberName == "Truncated").TypedValue.Value, Is.False);
    }
}

