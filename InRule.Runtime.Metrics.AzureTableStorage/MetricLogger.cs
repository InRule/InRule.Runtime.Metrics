using System;
using System.Configuration;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json.Linq;

namespace InRule.Runtime.Metrics.AzureTableStorage
{
	public sealed class MetricLogger : IMetricLogger
	{
		private const string AzureStorageConnectionStringKeyName = "inrule:runtime:metrics:azureTableStorage:connectionString";
		private const string AzureStorageTableName = "inrule:runtime:metrics:azureTableStorage:tableName";

		private readonly CloudTable _table;

		public MetricLogger()
		{
			CloudStorageAccount account = CloudStorageAccount.Parse(ConfigurationManager.AppSettings[AzureStorageConnectionStringKeyName]);
			CloudTableClient tableClient = account.CreateCloudTableClient();
			_table = tableClient.GetTableReference(ConfigurationManager.AppSettings[AzureStorageTableName]);
		}

		public async Task LogMetricsAsync(string serviceId, string ruleApplicationName, Guid sessionId, Metric[] metrics)
        {
            var batch = CreateTableBatchOperation(serviceId, ruleApplicationName, sessionId, metrics);

            await _table.ExecuteBatchAsync(batch);
        }

        public void LogMetrics(string serviceId, string ruleApplicationName, Guid sessionId, Metric[] metrics)
	    {
            var batch = CreateTableBatchOperation(serviceId, ruleApplicationName, sessionId, metrics);

            _table.ExecuteBatchAsync(batch).GetAwaiter().GetResult();
	    }

        private static TableBatchOperation CreateTableBatchOperation(string serviceId, string ruleApplicationName,
            Guid sessionId, Metric[] metrics)
        {
            var batch = new TableBatchOperation();
            foreach (Metric metric in metrics)
            {
                var jObject = new JObject();

                foreach (var metricProperty in metric.Schema.Properties)
                {
                    var value = metric[metricProperty];
                    jObject.Add(metricProperty.Name, new JObject(value));
                }

                batch.Add(TableOperation.Insert(new MetricEntity(serviceId, ruleApplicationName, sessionId.ToString(),
                    metric.EntityId.Replace('/', '_'), metric.EntityName, jObject.ToString())));
            }

            return batch;
        }
    }
}
