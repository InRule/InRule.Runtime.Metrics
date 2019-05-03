﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using DbUp;
using InRule.Repository;
using InRule.Runtime.Engine.State;

namespace InRule.Runtime.Metrics.SqlServer
{
    public sealed class MetricLogger : IMetricLogger
    {
        private const string SqlServerConnectionStringKeyName = "inrule:runtime:metrics:sqlServer:connectionString";
        private readonly string _connectionString;

        private static readonly Dictionary<DataType, SqlDbType> InRuleTypeToSqlTypeMap = new Dictionary<DataType, SqlDbType>
        {
            {DataType.String, SqlDbType.NVarChar},
            {DataType.Integer, SqlDbType.Int},
            {DataType.DateTime, SqlDbType.DateTime},
            {DataType.Date, SqlDbType.DateTime},
            {DataType.Number, SqlDbType.Decimal},
            {DataType.Boolean, SqlDbType.Bit},
        };

        private static readonly string[] CommonColumns = {
            "ServiceId",
            "RuleApplicationName",
            "SessionId",
            "IsRule",
            "EntityId",
            "MetricSchemaVersion"
        };

        private readonly SchemaService _schemaService;

        public MetricLogger() : this(ConfigurationManager.AppSettings[SqlServerConnectionStringKeyName])
        {

        }

        public MetricLogger(string connectionString)
        {
            _connectionString = connectionString;
            var upgrader =
                DeployChanges.To
                    .SqlDatabase(connectionString)
                    .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
                    .LogToAutodetectedLog()
                    .Build();

            var result = upgrader.PerformUpgrade();

            if (!result.Successful)
            {
                throw new Exception("Unable to upgrade the metric store to the latest schema.", result.Error);
            }

            _schemaService = new SchemaService(_connectionString);
        }

        public async Task LogMetricsAsync(string serviceId, string ruleApplicationName, Guid sessionId, Metric[] metrics)
        {
            if (metrics.Length < 1000)
            {
                await CopyToServerAsync(ruleApplicationName, metrics);
                return;
            }
            
            var entityNameToDataTableMap = LogMetricsNonAsyncWork(serviceId, ruleApplicationName, sessionId, metrics);

            await CopyToServerAsync(ruleApplicationName, entityNameToDataTableMap);
        }

        public void LogMetrics(string serviceId, string ruleApplicationName, Guid sessionId, Metric[] metrics)
        {
            if (metrics.Length < 1000)
            {
                CopyToServer(ruleApplicationName, metrics);
                return;
            }

            var entityNameToDataTableMap = LogMetricsNonAsyncWork(serviceId, ruleApplicationName, sessionId, metrics);

            CopyToServer(ruleApplicationName, entityNameToDataTableMap);
        }

        private Dictionary<string, DataTable> LogMetricsNonAsyncWork(string serviceId, string ruleApplicationName, Guid sessionId, Metric[] metrics)
        {
            var entityNameToDataTableMap = new Dictionary<string, DataTable>();

            var entityNameToSchemaMap = MetricsToDataTableConverter.ConvertMetricsToDataTables(serviceId, ruleApplicationName, sessionId, metrics, entityNameToDataTableMap);

            _schemaService.UpdateSchema(ruleApplicationName, entityNameToSchemaMap);
            return entityNameToDataTableMap;
        }

        private void CopyToServer(string ruleApplicationName, Metric[] metrics)
        {
            var entityToMetricMap = metrics.GroupBy(x => x.EntityName, x=> x, (key, group) => new { EntityName = key, Metrics = group.ToArray() });
            
            foreach (var entityNameWithMetrics in entityToMetricMap)
            {
                var entityName = entityNameWithMetrics.EntityName;
                var metricByEntityName = entityNameWithMetrics.Metrics[0];
                _schemaService.UpdateSchema(ruleApplicationName, entityName, metricByEntityName.Schema);

                var insertStatement = BuildParameterizedInsertStatement(ruleApplicationName, metricByEntityName.EntityName, metricByEntityName.Schema);

                using (var sqlConnection = new SqlConnection(_connectionString))
                using (var sqlCommand = new SqlCommand(insertStatement, sqlConnection))
                {
                    sqlConnection.Open();
                    foreach (var row in entityNameWithMetrics.Metrics)
                    {
                        PopulateInsertParameters(sqlCommand, row, metricByEntityName.Schema.Version);

                        sqlCommand.ExecuteNonQuery();
                    }
                    sqlConnection.Close();
                }
            }
        }

        private async Task CopyToServerAsync(string ruleApplicationName, Metric[] metrics)
        {
            var entityToMetricMap = metrics.GroupBy(x => x.EntityName, x => x, (key, group) => new {EntityName = key, Metrics = group.ToArray()});
            
            foreach (var entityNameWithMetrics in entityToMetricMap)
            {
                var entityName = entityNameWithMetrics.EntityName;
                var metricByEntityName = entityNameWithMetrics.Metrics[0];
                _schemaService.UpdateSchema(ruleApplicationName, entityName, metricByEntityName.Schema);

                var insertStatement = BuildParameterizedInsertStatement(ruleApplicationName, metricByEntityName.EntityName, metricByEntityName.Schema);
                
                using (var sqlConnection = new SqlConnection(_connectionString))
                using (var sqlCommand = new SqlCommand(insertStatement, sqlConnection))
                {
                    await sqlConnection.OpenAsync();
                    foreach (var row in entityNameWithMetrics.Metrics)
                    {
                        PopulateInsertParameters(sqlCommand, row, metricByEntityName.Schema.Version);

                        await sqlCommand.ExecuteNonQueryAsync();
                    }
                    sqlConnection.Close();
                }
            }
        }

        private void CopyToServer(string ruleApplicationName, Dictionary<string, DataTable> entityToDataTableMap)
        {
            foreach (var entityToDataTable in entityToDataTableMap)
            {
                using (var sqlConnection = new SqlConnection(_connectionString))
                using (var bulkCopy = new SqlBulkCopy(sqlConnection))
                {
                    bulkCopy.DestinationTableName = ruleApplicationName + "." + entityToDataTable.Key;
                    bulkCopy.WriteToServer(entityToDataTable.Value);
                }
            }
        }

        private async Task CopyToServerAsync(string ruleApplicationName, Dictionary<string, DataTable> entityToDataTableMap)
        {
            foreach (var entityToDataTable in entityToDataTableMap)
            {
                using (var sqlConnection = new SqlConnection(_connectionString))
                using (var bulkCopy = new SqlBulkCopy(sqlConnection))
                {
                    bulkCopy.DestinationTableName = ruleApplicationName + "." + entityToDataTable.Key;
                    await bulkCopy.WriteToServerAsync(entityToDataTable.Value);
                }
            }
        }

        private static string BuildParameterizedInsertStatement(string ruleApplicationName, string entityName, MetricSchema metricSchema)
        {
            var columnsString = new StringBuilder();
            var valuesString = new StringBuilder();
            columnsString.Append(@"INSERT INTO [" + ruleApplicationName + "].[");
            columnsString.Append(entityName);
            columnsString.Append("](");

            valuesString.AppendLine("VALUES (");

            foreach (var columnName in CommonColumns)
            {
                columnsString.AppendLine("[" + columnName + "],");
                valuesString.AppendLine("@" + columnName + ",");
            }

            foreach (var column in metricSchema)
            {
                columnsString.AppendLine("[" + column.GetMetricColumnName() + "],");
                valuesString.AppendLine("@" + column.GetMetricColumnName() + ",");
            }

            columnsString.Length -= (Environment.NewLine.Length + 1);
            valuesString.Length -= (Environment.NewLine.Length + 1);
            columnsString.Append(")");
            valuesString.AppendLine(")");

            var insertStatement = columnsString.Append(valuesString).ToString();
            return insertStatement;
        }

        private static void PopulateInsertParameters(SqlCommand sqlCommand, Metric row, int metricSchemaVersion)
        {
            sqlCommand.Parameters.Clear();

            sqlCommand.Parameters.Add("@ServiceId", SqlDbType.NVarChar).Value = row.ServiceId;
            sqlCommand.Parameters.Add("@RuleApplicationName", SqlDbType.NVarChar).Value = row.RuleApplicationName;
            sqlCommand.Parameters.Add("@SessionId", SqlDbType.NVarChar).Value = row.SessionId.ToString();
            sqlCommand.Parameters.Add("@EntityId", SqlDbType.NVarChar).Value = row.EntityId;           

            foreach (var column in row.Schema)
            {
                sqlCommand.Parameters.Add("@IsRule", SqlDbType.NVarChar).Value = column.IsRule;
                sqlCommand.Parameters.Add("@MetricSchemaVersion", SqlDbType.Int).Value = metricSchemaVersion;

                sqlCommand.Parameters.Add("@" + column.GetMetricColumnName(), InRuleTypeToSqlTypeMap[column.DataType]).Value =
                    row[column];
            }
        }
    }
}
