using HotChocolate;
using HotChocolate.Types;
using Loyalty.Api.Modules.RulesEngine.Domain;

namespace Loyalty.Api.Modules.RulesEngine.GraphQL;

/// <summary>GraphQL compatibility fields for points rules.</summary>
[ExtendObjectType(typeof(PointsRule))]
public sealed class PointsRuleCompatibilityExtensions
{
    /// <summary>
    /// Deprecated compatibility field retained for existing clients.
    /// Rule versions are no longer tracked because rule editing is disabled.
    /// </summary>
    [GraphQLName("ruleVersion")]
    public int GetRuleVersion([Parent] PointsRule rule) => 1;
}
