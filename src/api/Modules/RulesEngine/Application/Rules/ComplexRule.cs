using System.Text.Json.Nodes;
using Loyalty.Api.Modules.RulesEngine.Application.Invoices;
using Loyalty.Api.Modules.RulesEngine.Domain;

namespace Loyalty.Api.Modules.RulesEngine.Application.Rules;

/// <summary>Complex rule with nested condition groups.</summary>
public class ComplexRule : IInvoicePointsRule, IInvoicePointsRuleWithProductAttributes
{
    private readonly Guid _ruleId;
    private readonly int _rewardPoints;
    private readonly ComplexRuleEntityEvaluatorRegistry _entityEvaluators;
    private readonly ComplexRuleCompiledExpression _compiledExpression;
    private IReadOnlyDictionary<string, JsonObject> _productAttributesBySku =
        new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);

    public ComplexRule(
        Guid ruleId,
        Guid rootGroupId,
        int rewardPoints,
        IEnumerable<RuleConditionGroup> groups,
        IEnumerable<RuleCondition> conditions,
        IEnumerable<IComplexRuleEntityEvaluator>? entityEvaluators = null)
    {
        _ruleId = ruleId;
        _rewardPoints = rewardPoints;
        _entityEvaluators = new ComplexRuleEntityEvaluatorRegistry(entityEvaluators);
        _compiledExpression = ComplexRuleCompiler.Compile(rootGroupId, groups, conditions, _entityEvaluators);
    }

    public string Name => $"ComplexRule({_ruleId})";

    public int CalculatePoints(InvoiceUpsertRequest invoice)
    {
        if (_rewardPoints <= 0) return 0;
        if (!_compiledExpression.NodesByGroup.ContainsKey(_compiledExpression.RootGroupId)) return 0;

        var context = new ComplexRuleEvaluationContext(invoice, null, _productAttributesBySku);
        return EvaluateGroup(_compiledExpression.RootGroupId, context) ? _rewardPoints : 0;
    }

    public void SetProductAttributes(IReadOnlyDictionary<string, JsonObject> attributesBySku)
    {
        _productAttributesBySku = attributesBySku is null
            ? new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, JsonObject>(attributesBySku, StringComparer.OrdinalIgnoreCase);
    }

    private bool EvaluateGroup(Guid groupId, ComplexRuleEvaluationContext context)
    {
        if (!_compiledExpression.Groups.TryGetValue(groupId, out var group)) return false;
        if (!_compiledExpression.NodesByGroup.TryGetValue(groupId, out var nodes) || nodes.Count == 0) return false;

        var logic = group.Logic?.Trim().ToUpperInvariant() == "OR" ? "OR" : "AND";

        if (logic == "OR")
        {
            foreach (var node in nodes)
            {
                if (EvaluateNode(node, context)) return true;
            }
            return false;
        }

        if (context.Line != null)
        {
            foreach (var node in nodes)
            {
                if (!EvaluateNode(node, context)) return false;
            }
            return true;
        }

        var lineScopedNodes = nodes.Where(NodeRequiresLineScope).ToList();
        foreach (var node in nodes)
        {
            if (NodeRequiresLineScope(node)) continue;
            if (!EvaluateNode(node, context)) return false;
        }

        if (lineScopedNodes.Count == 0) return true;

        return context.Invoice.Lines.Any(lineItem =>
            lineScopedNodes.All(node => EvaluateNode(node, context.WithLine(lineItem))));
    }

    private bool EvaluateNode(ComplexRuleNode node, ComplexRuleEvaluationContext context)
    {
        if (node.IsGroup)
        {
            return EvaluateGroup(node.GroupId, context);
        }

        return node.Condition != null && EvaluateCondition(node.Condition, context);
    }

    private bool EvaluateCondition(RuleCondition condition, ComplexRuleEvaluationContext context)
    {
        if (!_entityEvaluators.TryGet(condition.EntityCode, out var evaluator))
            return false;

        if (evaluator.Scope == ComplexRuleEntityScope.InvoiceLine)
        {
            if (context.Line != null)
                return evaluator.Evaluate(condition, context);

            return context.Invoice.Lines.Any(lineItem => evaluator.Evaluate(condition, context.WithLine(lineItem)));
        }

        return evaluator.Evaluate(condition, context);
    }

    private bool NodeRequiresLineScope(ComplexRuleNode node)
    {
        if (node.IsGroup) return _compiledExpression.GroupsWithLineScopedConditions.Contains(node.GroupId);
        return node.Condition != null && _entityEvaluators.IsLineScoped(node.Condition.EntityCode);
    }
}
