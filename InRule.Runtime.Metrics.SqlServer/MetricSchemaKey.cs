using InRule.Runtime.Engine.State;

namespace InRule.Runtime.Metrics.SqlServer
{
    internal struct MetricSchemaKey
    {
        public string RuleApplicationName { get; }
        public string EntityName { get; }
        public MetricSchema MetricSchema { get; }

        public MetricSchemaKey(string ruleApplicationName, string entityName, MetricSchema metricSchema)
        {
            RuleApplicationName = ruleApplicationName;
            EntityName = entityName;
            MetricSchema = metricSchema;
        }

        public override int GetHashCode()
        {
            return RuleApplicationName.GetHashCode() *
                   EntityName.GetHashCode() *
                   MetricSchema.GetHashCode();
        }
    }
}