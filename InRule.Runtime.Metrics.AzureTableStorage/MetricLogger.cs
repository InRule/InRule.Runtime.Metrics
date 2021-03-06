﻿using System;
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

		public async Task LogMetricsAsync(string serviceName, string ruleApplicationName, Guid sessionId, Metric[] metrics)
        {
            var batch = CreateTableBatchOperation(serviceName, ruleApplicationName, sessionId, metrics);

            await _table.ExecuteBatchAsync(batch);
        }

        public void LogMetrics(string serviceName, string ruleApplicationName, Guid sessionId, Metric[] metrics)
	    {
            var batch = CreateTableBatchOperation(serviceName, ruleApplicationName, sessionId, metrics);

            _table.ExecuteBatchAsync(batch).GetAwaiter().GetResult();
	    }

		public static JToken GetMetricJsonValue(object value)
		{
			if (value == null) return null;

			switch (value)
			{
				case long longValue:
					return new JValue(longValue);
				case bool boolValue:
					return new JValue(boolValue);
				case string stringValue:
					return new JValue(stringValue);
				case decimal decimalValue:
					return new JValue(decimalValue);
				default:
					if (value is DateTime dateTimeValue)
					{
						return new JValue(dateTimeValue);
					}
					throw new NotSupportedException($"DataType {value.GetType().Name} not supported.");
			}
		}

        private static TableBatchOperation CreateTableBatchOperation(string serviceName, string ruleApplicationName, Guid sessionId, Metric[] metrics)
        {
            var batch = new TableBatchOperation();
            foreach (Metric metric in metrics)
            {
                var jObject = new JObject();

                foreach (var metricProperty in metric.Schema.Properties)
                {
                    object value = metric[metricProperty];
                    jObject.Add(metricProperty.Name, GetMetricJsonValue(value));
                }

                batch.Add(TableOperation.Insert(new MetricEntity(serviceName, ruleApplicationName, sessionId.ToString(), metric.EntityId.Replace('/', '_'), metric.EntityName, jObject.ToString())));
            }

            return batch;
        }
    }
}
