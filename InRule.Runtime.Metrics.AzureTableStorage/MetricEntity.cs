using Microsoft.WindowsAzure.Storage.Table;

namespace InRule.Runtime.Metrics.AzureTableStorage
{
	public sealed class MetricEntity : TableEntity
	{
		public MetricEntity(string serviceId, string ruleApplicationName, string sessionId, string entityId, string entityName, string metricJson) : base(sessionId, entityId)
		{
			ServiceId = serviceId;
			RuleApplicationName = ruleApplicationName;
			EntityName = entityName;
			MetricJson = metricJson;
		}

		public MetricEntity()
		{
		}

		public string ServiceId { get; set; }

		public string RuleApplicationName { get; set; }

		public string EntityName { get; set; }

		public string MetricJson { get; set; }
	}
}
