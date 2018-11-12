﻿using System;
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

		public Task LogMetric(string entityId, string entityName, string kpiJson)
		{
			return _table.ExecuteAsync(TableOperation.Insert(new MetricEntity(_serviceId, _ruleApplicationName, _sessionId, entityId.Replace('/', '_'), entityName, kpiJson)));
		}

		public async Task LogMetricBatch(IEnumerable<Runtime.MetricEntity> kpiEntities)
		{
			foreach (var kpi in kpiEntities)
			{
				await _table.ExecuteAsync(TableOperation.Insert(new MetricEntity(_serviceId, _ruleApplicationName, _sessionId, kpi.EntityId.Replace('/', '_'), kpi.EntityName, kpi.MetricJson)));
			}
		}

		public Task End()
		{
			return Task.CompletedTask;
		}
	}
}
