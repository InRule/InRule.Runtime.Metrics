using InRule.Repository;

namespace InRule.Runtime.Metrics.SqlServer.IntegrationTests
{
    public static class MetricExtensions
    {
        private static readonly XmlSerializableStringDictionary _attributes;
        private static readonly string _keyName = "isMetric";

        public static void SetAsMetric(this RuleRepositoryDefBase def, bool enabled)
        {
            var attributes = def.Attributes[RuleRepositoryDefBase.DefaultAttributeGroupKey];
            attributes[_keyName] = enabled.ToString();
        }
    }
}