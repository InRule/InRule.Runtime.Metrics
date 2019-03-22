using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Text;
using InRule.Repository.RuleElements;
using InRule.Runtime.Engine.State;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using DataType = InRule.Repository.DataType;

namespace InRule.Runtime.Metrics.SqlServer
{
    internal class SchemaService : IDisposable
    {
        private readonly Dictionary<Type, Microsoft.SqlServer.Management.Smo.DataType> _frameworkTypeToSmoTypeMap = new Dictionary<Type, Microsoft.SqlServer.Management.Smo.DataType>
        {
            {typeof(string), Microsoft.SqlServer.Management.Smo.DataType.NVarCharMax},
            {typeof(Int32), Microsoft.SqlServer.Management.Smo.DataType.Int},
            {typeof(DateTime), Microsoft.SqlServer.Management.Smo.DataType.DateTime},
            {typeof(decimal), Microsoft.SqlServer.Management.Smo.DataType.Decimal(30, 20)},
            {typeof(bool), Microsoft.SqlServer.Management.Smo.DataType.Bit},
            {typeof(Guid), Microsoft.SqlServer.Management.Smo.DataType.NVarCharMax},
        };

        private readonly Dictionary<DataType, Microsoft.SqlServer.Management.Smo.DataType> _inRuleToSqlTypeMap = new Dictionary<DataType, Microsoft.SqlServer.Management.Smo.DataType>
        {
            {DataType.String, Microsoft.SqlServer.Management.Smo.DataType.NVarCharMax},
            {DataType.Integer, Microsoft.SqlServer.Management.Smo.DataType.Int},
            {DataType.Date, Microsoft.SqlServer.Management.Smo.DataType.DateTime},
            {DataType.DateTime, Microsoft.SqlServer.Management.Smo.DataType.DateTime},
            {DataType.Number, Microsoft.SqlServer.Management.Smo.DataType.Decimal(16, 38)},
            {DataType.Boolean, Microsoft.SqlServer.Management.Smo.DataType.Bit}
        };

        private readonly List<(string, Type)> _commonColumns = new List<(string, Type)>
        {
            ("ServiceId", typeof(string)),
            ("RuleApplicationName", typeof(string)),
            ("SessionId", typeof(string))
        };

        private readonly HashSet<MetricSchemaKey> _ruleAppEntityToSchemaHashMap = new HashSet<MetricSchemaKey>();

        private readonly string _connectionString;
        private SqlConnection _connection;

        public SchemaService(string connectionString)
        {
            _connectionString = connectionString;
        }

        private SqlConnection Connection => _connection ?? (_connection = new SqlConnection(_connectionString));

        public void UpdateSchema(string ruleApplicationName, Dictionary<string, MetricSchema> entitiesInMetrics)
        {

            using (Connection)
            {
                foreach (var tableToValidateSchema in entitiesInMetrics)
                {
                    var tableName = ruleApplicationName + "." + tableToValidateSchema.Key;
                    var metricSchema = tableToValidateSchema.Value;

                    if (!HasSchemaChanged(ruleApplicationName, tableName, tableToValidateSchema.Value))
                        continue;

                    var server = new Server(new ServerConnection(Connection));
                    var database = server.Databases[Connection.Database];
                    var schema = GetOrAddSchema(ruleApplicationName, database);
                    var table = GetOrAddTable(database, tableName);

                    foreach (var metricProperty in metricSchema)
                    {
                        GetOrAddColumn(metricProperty, table);
                    }
                }
            }
        }

        private static Schema GetOrAddSchema(string ruleApplicationName, Database database)
        {
            if (!database.Schemas.Contains(ruleApplicationName))
                return database.Schemas[ruleApplicationName];
            
            var schema = new Schema(database, ruleApplicationName);
            schema.Create();
            database.Schemas.Add(schema);
            return schema;
        }

        private Column GetOrAddColumn(MetricProperty metricProperty, Table table)
        {
            var columnName = metricProperty.GetMetricColumnName();

            if (table.Columns.Contains(columnName))
                return table.Columns[columnName];

            var column = new Column(table, columnName, _inRuleToSqlTypeMap[metricProperty.DataType]);
            column.Create();

            return column;
        }

        private Table GetOrAddTable(Database database, string tableName)
        {
            Table table;
            if (!database.Tables.Contains(tableName))
            {
                table = new Table(database, tableName);
                foreach (var (columnName, type) in _commonColumns)
                {
                    var column = new Column(table, columnName, _frameworkTypeToSmoTypeMap[type]);
                    table.Columns.Add(column);
                }

                table.Create();
            }
            else
            {
                table = database.Tables[tableName];
            }

            return table;
        }

        private bool HasSchemaChanged(string ruleAppName, string entityName, MetricSchema newMetricSchema)
        {
            var metricSchemaKey = new MetricSchemaKey(ruleAppName, entityName, newMetricSchema);
            if (_ruleAppEntityToSchemaHashMap.Contains(metricSchemaKey))
            {
                //seen this schema before in this session;
                return false;
            }
            
            _ruleAppEntityToSchemaHashMap.Add(metricSchemaKey);

            if(Connection.State != ConnectionState.Open)
                Connection.Open();

            var selectFromMetricSchema = "SELECT MetricSchema from MetricSchemaStore where RuleAppName = @RuleAppName AND EntityName = @EntityName";

            var command = Connection.CreateCommand();
            command.CommandText = selectFromMetricSchema;
            command.Parameters.Add("@RuleAppName", SqlDbType.NVarChar).Value = ruleAppName;
            command.Parameters.Add("@EntityName", SqlDbType.NVarChar).Value = entityName;

            var metricJson = command.ExecuteScalar().ToString();
            if (metricJson == null)
            {
                SerializeSchemaToDbCache(ruleAppName, entityName, newMetricSchema.GetJson(), true);
                return true;
            }
            
            var oldSchema = MetricSchema.FromJson(new StringReader(metricJson));
            
            if(oldSchema.GetHashCode() != newMetricSchema.GetHashCode() 
                || !oldSchema.Equals(newMetricSchema))
            {
                SerializeSchemaToDbCache(ruleAppName, entityName, newMetricSchema.GetJson(), false);

                return true;
            }

            return false;
        }

        private void SerializeSchemaToDbCache(string ruleAppName, string entityName, string metricJson, bool isNew)
        {
            string storageSql = "";
            
            if (isNew)
            {
                storageSql =
                    "INSERT INTO MetricSchemaStore (RuleAppName, EntityName, MetricSchema) VALUES(@RuleAppName,@EntityName, @MetricSchema)";
            }
            else
            {
                storageSql = "UPDATE MetricSchemaStore SET MetricSchema = @MetricSchema where RuleAppName = @RuleAppName AND EntityName = @EntityName";
            }

            var command = Connection.CreateCommand();
            command.CommandText = storageSql;
            command.Parameters.Add("@RuleAppName", SqlDbType.NVarChar).Value = ruleAppName;
            command.Parameters.Add("@EntityName", SqlDbType.NVarChar).Value = entityName;
            command.Parameters.Add("@MetricSchema", SqlDbType.NVarChar).Value = metricJson;
        }

        public void Dispose()
        {
            _connection?.Dispose();
        }
    }
}