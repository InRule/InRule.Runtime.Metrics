using InRule.Runtime.Engine.State;
using Microsoft.WindowsAzure.Storage.Table;

namespace InRule.Runtime.Metrics.AzureTableStorage
{
	public sealed class MetricEntity : TableEntity
	{
		public MetricEntity(string serviceName, string ruleApplicationName, string sessionId, string entityId, string entityName, string metricJson) : base(sessionId, entityId)
		{
			Version = MetricSchema.CurrentVersion;
			ServiceName = serviceName;
			RuleApplicationName = ruleApplicationName;
			EntityName = entityName;
			MetricJson = metricJson;
		}

		public MetricEntity()
		{
		}

		public int Version { get; set; }

		public string ServiceName { get; set; }

		public string RuleApplicationName { get; set; }

		public string EntityName { get; set; }

		public string MetricJson { get; set; }
	}
}
