using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using DbUp;

namespace InRule.Runtime.Metrics.SqlServer
{
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

            var entityNameToSchemaMap = MetricsToDataTableConverter.ConvertMetricsToDataTables(serviceId, ruleApplicationName, sessionId, metrics, entityNameToDataTableMap);

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
                    await sqlConnection.OpenAsync();
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
            columnsString.Append(@"INSERT INTO [" + ruleApplicationName + "].[");
            columnsString.Append(entityToDataTable.Key);
            columnsString.Append("](");

            valuesString.AppendLine("VALUES (");


            foreach (DataColumn column in entityToDataTable.Value.Columns)
            {
                columnsString.AppendLine("[" + column.ColumnName + "],");
                valuesString.AppendLine("@" + column.ColumnName + ",");
            }

            columnsString.Length -= (Environment.NewLine.Length + 1);
            valuesString.Length -= (Environment.NewLine.Length + 1);
            columnsString.Append(")");
            valuesString.AppendLine(")");

            var insertStatement = columnsString.Append(valuesString).ToString();
            return insertStatement;
        }
    }
}
