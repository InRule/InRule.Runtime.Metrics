using System;
using System.Collections.Generic;
using System.Configuration;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

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

		public async Task LogMetrics(string serviceId, string ruleApplicationName, Guid sessionId, IEnumerable<Metric> metrics)
		{
			var batch = new TableBatchOperation();
			foreach (Metric metric in metrics)
			{
				batch.Add(TableOperation.Insert(new MetricEntity(serviceId, ruleApplicationName, sessionId.ToString(), metric.EntityId.Replace('/', '_'), metric.EntityName, metric.MetricJson)));
			}

			await _table.ExecuteBatchAsync(batch);
		}
	}
}
