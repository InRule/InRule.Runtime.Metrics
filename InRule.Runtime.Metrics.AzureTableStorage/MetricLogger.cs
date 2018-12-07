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
		private string _serviceId;
		private string _ruleApplicationName;
		private string _sessionId;

		public MetricLogger()
		{
			CloudStorageAccount account = CloudStorageAccount.Parse(ConfigurationManager.AppSettings[AzureStorageConnectionStringKeyName]);
			CloudTableClient tableClient = account.CreateCloudTableClient();
			_table = tableClient.GetTableReference(ConfigurationManager.AppSettings[AzureStorageTableName]);
		}

		public Task Start(string serviceId, string ruleApplicationName, Guid sessionId)
		{
			_serviceId = serviceId;
			_ruleApplicationName = ruleApplicationName;
			_sessionId = sessionId.ToString();
			return Task.CompletedTask;
		}

		public Task LogMetric(Metric metric)
		{
			return _table.ExecuteAsync(TableOperation.Insert(new MetricEntity(_serviceId, _ruleApplicationName, _sessionId, metric.EntityId.Replace('/', '_'), metric.EntityName, metric.MetricJson)));
		}

		public async Task LogMetricBatch(IEnumerable<Metric> metrics)
		{
			var batch = new TableBatchOperation();
			foreach (Metric metric in metrics)
			{
				batch.Add(TableOperation.Insert(new MetricEntity(_serviceId, _ruleApplicationName, _sessionId, metric.EntityId.Replace('/', '_'), metric.EntityName, metric.MetricJson)));
			}

			await _table.ExecuteBatchAsync(batch);
		}

		public Task End()
		{
			return Task.CompletedTask;
		}
	}
}
