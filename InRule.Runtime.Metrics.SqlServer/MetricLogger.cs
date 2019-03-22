using InRule.Runtime.Engine.State;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DataType = InRule.Repository.DataType;

namespace InRule.Runtime.Metrics.SqlServer
{
    internal sealed class NoOpLogger : IMetricLogger
    {
        public Task LogMetricsAsync(string serviceId, string ruleApplicationName, Guid sessionId, Metric[] metrics)
        {
            return Task.CompletedTask;
        }

        public void LogMetrics(string serviceId, string ruleApplicationName, Guid sessionId, Metric[] metrics)
        {

        }
    }


    public sealed class MetricLogger : IMetricLogger
    {
        private const string SqlServerConnectionStringKeyName = "inrule:runtime:metrics:sqlServer:connectionString";
        private readonly string _connectionString;

        private readonly Dictionary<Type, SqlDbType> _frameworkTypeToSqlTypeMap = new Dictionary<Type, SqlDbType>
        {
            {typeof(string), SqlDbType.NVarChar},
            {typeof(Int32), SqlDbType.Int},
            {typeof(DateTime), SqlDbType.DateTime},
            {typeof(decimal), SqlDbType.Decimal},
            {typeof(bool), SqlDbType.Bit},
            {typeof(Guid), SqlDbType.NVarChar},
        };

        public MetricLogger()
        {
            _connectionString = ConfigurationManager.AppSettings[SqlServerConnectionStringKeyName];

        }

        public MetricLogger(string connectionString)
        {
            _connectionString = connectionString;
        }


        public async Task LogMetricsAsync(string serviceId, string ruleApplicationName, Guid sessionId, Metric[] metrics)
        {
            var entityNameToDataTableMap = LogMetricsNonAsyncWork(serviceId, ruleApplicationName, sessionId, metrics);

            await CopyToServerAsync(ruleApplicationName, entityNameToDataTableMap);
        }


        public void LogMetrics(string serviceId, string ruleApplicationName, Guid sessionId, Metric[] metrics)
        {
            var entityNameToDataTableMap = LogMetricsNonAsyncWork(serviceId, ruleApplicationName, sessionId, metrics);

            CopyToServer(ruleApplicationName, entityNameToDataTableMap);
        }

        private Dictionary<string, DataTable> LogMetricsNonAsyncWork(string serviceId, string ruleApplicationName, Guid sessionId,
            Metric[] metrics)
        {
            var entityNameToDataTableMap = new Dictionary<string, DataTable>();

            var entityNameToSchemaMap = MeticsToDataTableConverter.ConvertMetricsToDataTables(serviceId, ruleApplicationName, sessionId, metrics, entityNameToDataTableMap);

            var schemaService = new SchemaService(_connectionString);

            schemaService.UpdateSchema(ruleApplicationName, entityNameToSchemaMap);
            return entityNameToDataTableMap;
        }

        private void CopyToServer(string ruleApplicationName, Dictionary<string, DataTable> entityToDataTableMap)
        {
            foreach (var entityToDataTable in entityToDataTableMap)
            {
                var insertStatement = BuildInsertStatement(ruleApplicationName, entityToDataTable);


                using (var sqlConnection = new SqlConnection(_connectionString))
                using (var sqlCommand = new SqlCommand(insertStatement, sqlConnection))
                {
                    sqlConnection.Open();
                    foreach (DataRow row in entityToDataTable.Value.Rows)
                    {
                        sqlCommand.Parameters.Clear();
                        foreach (DataColumn column in entityToDataTable.Value.Columns)
                        {
                            sqlCommand.Parameters.Add("@" + column.ColumnName, _frameworkTypeToSqlTypeMap[column.DataType]).Value =
                                row[column.ColumnName];
                        }

                        sqlCommand.ExecuteNonQuery();
                    }
                    sqlConnection.Close();
                }
            }
        }

        private async Task CopyToServerAsync(string ruleApplicationName, Dictionary<string, DataTable> entityToDataTableMap)
        {
            foreach (var entityToDataTable in entityToDataTableMap)
            {
                var insertStatement = BuildInsertStatement(ruleApplicationName, entityToDataTable);


                using (var sqlConnection = new SqlConnection(_connectionString))
                using (var sqlCommand = new SqlCommand(insertStatement, sqlConnection))
                {
                    sqlConnection.Open();
                    foreach (DataRow row in entityToDataTable.Value.Rows)
                    {
                        sqlCommand.Parameters.Clear();
                        foreach (DataColumn column in entityToDataTable.Value.Columns)
                        {
                            sqlCommand.Parameters.Add("@" + column.ColumnName, _frameworkTypeToSqlTypeMap[column.DataType]).Value =
                                row[column.ColumnName];
                        }

                        await sqlCommand.ExecuteNonQueryAsync();
                    }
                    sqlConnection.Close();
                }
            }
        }

        private static string BuildInsertStatement(string ruleApplicationName, KeyValuePair<string, DataTable> entityToDataTable)
        {
            var columnsString = new StringBuilder();
            var valuesString = new StringBuilder();
            columnsString.Append(@"INSERT INTO [+" + ruleApplicationName + "].[");
            columnsString.Append(ruleApplicationName + "_" + entityToDataTable.Key);
            columnsString.Append("](");

            valuesString.AppendLine("VALUES (");


            foreach (DataColumn column in entityToDataTable.Value.Columns)
            {
                columnsString.AppendLine("[" + column.ColumnName + "],");
                valuesString.AppendLine("@" + column.ColumnName + ",");
            }

            columnsString.Length = columnsString.Length - (Environment.NewLine.Length + 1);
            valuesString.Length = valuesString.Length - (Environment.NewLine.Length + 1);
            columnsString.Append(")");
            valuesString.AppendLine(")");

            var insertStatement = columnsString.Append(valuesString).ToString();
            return insertStatement;
        }
    }

    public class MeticsToDataTableConverter
    {
        private static readonly Dictionary<DataType, Type> _inRuleToFrameworkTypeMap = new Dictionary<DataType, Type>
        {
            {DataType.String, typeof(string)},
            {DataType.Integer, typeof(int)},
            {DataType.Date, typeof(DateTime)},
            {DataType.DateTime, typeof(DateTime)},
            {DataType.Number, typeof(decimal)},
            {DataType.Boolean, typeof(bool)}
        };

        private static readonly List<(string, Type)> _commonColumns = new List<(string, Type)>
        {
            ("ServiceId", typeof(string)),
            ("RuleApplicationName", typeof(string)),
            ("SessionId", typeof(string))
        };


        public static Dictionary<string, MetricSchema> ConvertMetricsToDataTables(string serviceId, string ruleApplicationName, Guid sessionId,
    IEnumerable<Metric> metrics, Dictionary<string, DataTable> entityToDataTableMap)
        {
            Dictionary<string, MetricSchema> mapOfTablesToCheck = new Dictionary<string, MetricSchema>();
            foreach (var metric in metrics)
            {
                if (GetOrCreateDataTable(entityToDataTableMap, metric, out DataTable dataTable))
                {
                    mapOfTablesToCheck.Add(metric.EntityName, metric.Schema);
                }

                var metricRow = dataTable.NewRow();
                metricRow["ServiceId"] = serviceId;
                metricRow["RuleApplicationName"] = ruleApplicationName;
                metricRow["SessionId"] = sessionId.ToString();

                foreach (var metricProperty in metric.Schema.Properties)
                {
                    var value = metric[metricProperty] ?? DBNull.Value;
                    metricRow[metricProperty.GetMetricColumnName()] = value;
                }

                dataTable.Rows.Add(metricRow);
            }

            return mapOfTablesToCheck;
        }



        /// <summary>
        /// Returns true if the DataTable needed to be created, false if the DataTable already existed.
        /// </summary>
        /// <param name="entityToDataTableMap"></param>
        /// <param name="metric"></param>
        /// <param name="dataTable"></param>
        /// <returns></returns>
        private static bool GetOrCreateDataTable(Dictionary<string, DataTable> entityToDataTableMap, Metric metric, out DataTable dataTable)
        {
            if (entityToDataTableMap.TryGetValue(metric.EntityName, out dataTable))
            {
                return false;
            }

            dataTable = new DataTable();

            foreach (var (columnName, type) in _commonColumns)
            {
                dataTable.Columns.Add(columnName, type);
            }

            foreach (var metricProperty in metric.Schema)
            {
                dataTable.Columns.Add(metricProperty.GetMetricColumnName(), _inRuleToFrameworkTypeMap[metricProperty.DataType]);
            }

            entityToDataTableMap.Add(metric.EntityName, dataTable);
            return true;

        }

    }

    public static class Extensions
    {
        public static string GetMetricColumnName(this MetricProperty property)
        {

            return $"{property.Name}_{property.DataType}";
        }
    }

}
