using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Editor.Tests;

public class ExpressionInsertionTests
{
    private static readonly ExpressionChoice Data = new("Display data", "amount", "", "amount");
    private static ExpressionChoice Function(string name) => ExpressionInsertion.Functions.Single(f => f.Function == name);
    private static string Insert(string marked, ExpressionChoice choice, string attribute = "color")
    {
        var caret = marked.IndexOf('|');
        return ExpressionInsertion.Insert(marked.Replace("|", ""), caret, caret, attribute, choice).Text;
    }
    [TestCase("|", "{{amount}}")]
    [TestCase("Text | suffix", "Text {{amount}} suffix")]
    [TestCase("{{=setAlpha(color, |)}}", "{{=setAlpha(color, amount)}}")]
    [TestCase("{{|}}", "{{amount}}")]
    [TestCase("{{co|lor}}", "{{amount}}")]
    [TestCase("{{value|:F1}}", "{{amount:F1}}")]
    [TestCase("{{a}} | {{b}}", "{{a}} {{amount}} {{b}}")]
    [TestCase("{{a}}|", "{{a}}{{amount}}")]
    public void VariableRespectsBindingBoundaries(string source, string expected)
        => Assert.That(Insert(source, Data), Is.EqualTo(expected));

    [Test] public void FunctionWrapsDirectBindingAndKeepsFormat()
        => Assert.That(Insert("Before {{value|:F1}} after", Function("round")), Is.EqualTo("Before {{= round(value, 0):F1}} after"));
    [Test] public void FunctionInsideExpressionHasNoExtraDelimiters()
        => Assert.That(Insert("{{= max(|, 1)}}", Function("floor")), Is.EqualTo("{{= max(floor(0), 1)}}"));
    [Test] public void FunctionSelectsItsFirstArgument()
    {
        var result = ExpressionInsertion.Insert("", 0, 0, "color", Function("setAlpha"));
        Assert.That(result.Text, Is.EqualTo("{{= setAlpha('#ffffff', 0.5)}}"));
        Assert.That(result.Text[result.Start..result.End], Is.EqualTo("'#ffffff'"));
        Assert.That(ExpressionInsertion.Insert(result.Text, result.Start, result.End, "color", Data).Text, Is.EqualTo("{{= setAlpha(amount, 0.5)}}"));
    }
    [TestCase("{{value}}", "{{= floor(value)}}")]
    [TestCase("{{= value + 1}}", "{{=floor(value + 1)}}")]
    public void SelectedWholeBindingIsWrapped(string text, string expected)
        => Assert.That(ExpressionInsertion.Insert(text, 0, text.Length, "size", Function("floor")).Text, Is.EqualTo(expected));
    [Test] public void SelectedExpressionBecomesFirstArgument()
        => Assert.That(ExpressionInsertion.Insert("{{= value / 100}}", 4, 15, "size", Function("floor")).Text, Is.EqualTo("{{= floor(value / 100)}}"));
    [TestCase("{{amount:F|1}}")]
    [TestCase("{{= value| ")]
    [TestCase("{|{value}}")]
    [TestCase("{{= mixColor('#ff|ffff', '#000000', 0.5)}}")]
    public void UnsafeInsertionIsRejected(string source)
        => Assert.Throws<FormatException>(() => Insert(source, Data));
    [Test] public void SelectionCrossingBindingsIsRejected()
        => Assert.Throws<FormatException>(() => ExpressionInsertion.Insert("{{a}} {{b}}", 3, 9, "size", Data));
    [Test] public void ConditionsAndKeysAreBareAndRestricted()
    {
        Assert.That(Insert("|", Data, "key"), Is.EqualTo("amount"));
        Assert.That(Insert("| > 0", Data, "when"), Is.EqualTo("amount > 0"));
        Assert.That(Insert("|", Function("number"), "when"), Is.EqualTo("number(value, 0)"));
        Assert.Throws<FormatException>(() => Insert("|", Function("floor"), "when"));
        Assert.Throws<FormatException>(() => Insert("|", Function("floor"), "key"));
        Assert.Throws<FormatException>(() => Insert("|", Function("type")));
    }
    [Test] public void HyphenatedKeysWorkDirectlyButAreNotSilentlySubtracted()
    {
        var key = Data with { First = "my-key" };
        Assert.That(Insert("{{value|}}", key), Is.EqualTo("{{my-key}}"));
        Assert.Throws<FormatException>(() => Insert("{{= |}}", key));
        Assert.Throws<FormatException>(() => Insert("{{my-key|}}", Function("floor")));
    }
    [Test] public void AllCalculationExamplesEvaluate()
    {
        var values = DataHub.ParseValues("""{"value":1,"title":"Song","text":"a,b","rows":[1,"two"]}""");
        foreach (var choice in ExpressionInsertion.Functions.Where(f => f.Function != "type"))
            Assert.DoesNotThrow(() => DisplayMath.Evaluate(choice.Example, values), choice.Label);
        foreach (var choice in ExpressionInsertion.Functions.Where(f => f.Condition))
            Assert.DoesNotThrow(() => DisplayCondition.Evaluate(choice.Example, values), choice.Label);
    }

    [TestCase("color", "expr + expr", "{{= 0 + 1}}", "0")]
    [TestCase("when", "expr >= expr", "0 >= 1", "0")]
    [TestCase("visibleWhen", "!expr", "!true", "true")]
    public void OperatorUsesTheFieldSyntaxAndSelectsTheFirstOperand(string field, string label, string expected, string selected)
    {
        var choice = ExpressionInsertion.Operators.Single(c => c.Label == label);
        var result = ExpressionInsertion.Insert("", 0, 0, field, choice);
        Assert.That(result.Text, Is.EqualTo(expected));
        Assert.That(result.Text[result.Start..result.End], Is.EqualTo(selected));
    }

    [TestCase("{{value:F1}}", "{{value:F1}}", "expr + expr", "{{= value + 1:F1}}", "size")]
    [TestCase("{{= 2 * 3}}", "3", "expr + expr", "{{= 2 * (3 + 1)}}", "size")]
    [TestCase("{{= a + b:F1}}", "a + b", "expr * expr", "{{= (a + b) * 1:F1}}", "size")]
    [TestCase("a or b", "a or b", "!expr", "!(a or b)", "when")]
    [TestCase("length(title)", "length(title)", "expr > expr", "length(title) > 1", "when")]
    public void WrappingWithAnOperatorPreservesOperandsAndSurroundings(string text, string selection, string label, string expected, string field)
    {
        var start = text.IndexOf(selection, StringComparison.Ordinal);
        var choice = ExpressionInsertion.Operators.Single(c => c.Label == label);
        Assert.That(ExpressionInsertion.Insert(text, start, start + selection.Length, field, choice).Text, Is.EqualTo(expected));
    }

    [Test] public void OperatorsOfferedInEachFieldAreAcceptedByItsEvaluator()
    {
        var data = DataHub.ParseValues("{}");
        foreach (var choice in ExpressionInsertion.Operators)
        {
            if (ExpressionInsertion.Supports("size", choice)) Assert.DoesNotThrow(() => DisplayMath.Evaluate(choice.Example, data), choice.Label);
            if (ExpressionInsertion.Supports("when", choice)) Assert.DoesNotThrow(() => DisplayCondition.Evaluate(choice.Example, data), choice.Label);
            Assert.Throws<FormatException>(() => ExpressionInsertion.Insert("", 0, 0, "key", choice));
        }
        Assert.Throws<FormatException>(() => Insert("|", ExpressionInsertion.Operators.Single(c => c.Label == "expr + expr"), "when"));
        Assert.Throws<FormatException>(() => Insert("|", ExpressionInsertion.Operators.Single(c => c.Label == "expr == expr")));
    }
}
