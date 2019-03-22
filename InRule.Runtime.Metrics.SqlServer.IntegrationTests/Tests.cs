using System;
using InRule.Repository;
using InRule.Repository.Infos;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using NUnit.Framework;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using DataType = InRule.Repository.DataType;

namespace InRule.Runtime.Metrics.SqlServer.IntegrationTests
{
    public class Tests
    {
        private const string ServerConnectionString = "Server=(localdb)\\MSSQLLocalDB;Integrated Security=true";
        private const string IntegrationTestDatabaseName = "MetricTesting";

        private const string DatabaseConnectionString = ServerConnectionString + ";Initial Catalog=" + IntegrationTestDatabaseName;

        [SetUp]
        public void Setup()
        {
            Console.WriteLine("Setup.");
            var sqlConnection = new SqlConnection(ServerConnectionString);
            var server = new Server(new ServerConnection(sqlConnection));
            if (server.Databases.Contains(IntegrationTestDatabaseName))
            {
                return;
            }

            var database = new Database(server, IntegrationTestDatabaseName);
            database.Create();
        }

        [TearDown]
        public void TearDown()
        {
            Console.WriteLine("Teardown.");
            var sqlConnection = new SqlConnection(ServerConnectionString);
            var server = new Server(new ServerConnection(sqlConnection));

            if (!server.Databases.Contains(IntegrationTestDatabaseName))
            {
                return;
            }

            server.KillAllProcesses(IntegrationTestDatabaseName);
            server.KillDatabase(IntegrationTestDatabaseName);
        }

        [Test]
        public void GivenMetrics_MetricsAreStored()
        {
            var ruleAppDef = new RuleApplicationDef("TestRuleApplication");
            var entity1Def = ruleAppDef.AddEntity("Entity1");
            var calc1Def = entity1Def.AddField("Field1", DataType.Integer, "1");
            calc1Def.SetAsMetric(true);

            using (var session = new RuleSession(ruleAppDef))
            {
                session.Settings.MetricLogger = new MetricLogger(DatabaseConnectionString);
                session.Settings.MetricServiceName = "Integration Tests";

                session.CreateEntity(entity1Def.Name);

                session.ApplyRules();
            }
        }

        [TestCase(DataType.Boolean, "true", SqlDataType.Bit)]
        [TestCase(DataType.Integer, "123", SqlDataType.Int)]
        [TestCase(DataType.Number, "123.123", SqlDataType.Decimal)]
        [TestCase(DataType.Date, "#2019-01-02#", SqlDataType.DateTime)]
        [TestCase(DataType.DateTime, "#2019-01-02T01:02:03#", SqlDataType.DateTime)]
        [TestCase(DataType.String, "\"Test\"", SqlDataType.NVarCharMax)]
        public void MetricDataTypes_CreatesCorrectSqlDataTypes(DataType dataType, string defaultValue, SqlDataType sqlDataType)
        {
            Console.WriteLine("Test.");
            var ruleAppDef = new RuleApplicationDef("TestRuleApplication");
            var entity1Def = ruleAppDef.AddEntity("Entity1");
            var field1Def = entity1Def.AddField("Field1", dataType, defaultValue);
            field1Def.SetAsMetric(true);

            using (var session = new RuleSession(ruleAppDef))
            {
                session.Settings.MetricLogger = new MetricLogger(DatabaseConnectionString);
                session.Settings.MetricServiceName = "Integration Tests";

                session.CreateEntity(entity1Def.Name);

                session.ApplyRules();
            }

            var table = GetTable(ruleAppDef, entity1Def);
            var column = table.Columns[field1Def.Name + "_" + dataType];

            Assert.That(column.DataType.SqlDataType, Is.EqualTo(sqlDataType));
        }

        [Test]
        public void EntitySchemaChanges_WithNewLogger_DatabaseTablesSchemaChanges()
        {
            var ruleAppDef = new RuleApplicationDef("TestRuleApplication");
            var entity1Def = ruleAppDef.AddEntity("Entity1");
            var field1Def = entity1Def.AddField("Field1", DataType.Integer, "1");
            field1Def.SetAsMetric(true);

            using (var session = new RuleSession(ruleAppDef))
            {
                session.Settings.MetricLogger = new MetricLogger(DatabaseConnectionString);
                session.Settings.MetricServiceName = "Integration Tests";

                session.CreateEntity(entity1Def.Name);

                session.ApplyRules();
            }

            var table = GetTable(ruleAppDef, entity1Def);
            var column = table.Columns[field1Def.Name + "_" + DataType.Integer];

            Assert.That(column.DataType.SqlDataType, Is.EqualTo(SqlDataType.Int));

            field1Def.DataType = DataType.String;

            using (var session = new RuleSession(ruleAppDef))
            {
                session.Settings.MetricLogger = new MetricLogger(DatabaseConnectionString);
                session.Settings.MetricServiceName = "Integration Tests";

                session.CreateEntity(entity1Def.Name);

                session.ApplyRules();
            }

            column = table.Columns[field1Def.Name + "_" + DataType.String];

            Assert.That(column.DataType.SqlDataType, Is.EqualTo(SqlDataType.NVarCharMax));
        }

        [Test]
        public void EntitySchemaChanges_ReusingLoggerInstance_DatabaseTablesSchemaChanges()
        {
            var ruleAppDef = new RuleApplicationDef("TestRuleApplication");
            var entity1Def = ruleAppDef.AddEntity("Entity1");
            var field1Def = entity1Def.AddField("Field1", DataType.Integer, "1");
            field1Def.SetAsMetric(true);

            var metricLogger = new MetricLogger(DatabaseConnectionString);
            using (var session = new RuleSession(ruleAppDef))
            {
                session.Settings.MetricLogger = metricLogger;
                session.Settings.MetricServiceName = "Integration Tests";

                session.CreateEntity(entity1Def.Name);

                session.ApplyRules();
            }

            var table = GetTable(ruleAppDef, entity1Def);
            var column = table.Columns[field1Def.Name + "_" + DataType.Integer];

            Assert.That(column.DataType.SqlDataType, Is.EqualTo(SqlDataType.Int));

            field1Def.DataType = DataType.String;

            using (var session = new RuleSession(ruleAppDef))
            {
                session.Settings.MetricLogger = metricLogger;
                session.Settings.MetricServiceName = "Integration Tests";

                session.CreateEntity(entity1Def.Name);

                session.ApplyRules();
            }

            column = table.Columns[field1Def.Name + "_" + DataType.String];

            Assert.That(column.DataType.SqlDataType, Is.EqualTo(SqlDataType.NVarCharMax));
        }

        private static Table GetTable(RuleApplicationDef ruleAppDef, EntityDef entity1Def)
        {
            var sqlConnection = new SqlConnection(ServerConnectionString);
            var server = new Server(new ServerConnection(sqlConnection));

            var database = server.Databases[IntegrationTestDatabaseName];
            var table = database.Tables[ruleAppDef.Name + "_" + entity1Def.Name];

            return table;
        }


        [Test]
        [Explicit]
        public void Adhoc()
        {
            var ruleAppDef = RuleApplicationDef.Load("InvoiceForKpis.ruleappx");

            var entityState = new string[100];

            for (int i = 1; i < 101; i++)
            {
                entityState[i-1] = File.ReadAllText("InvoiceJsonFiles\\Invoice" + i + ".json");
            }

            var stopWatch = new Stopwatch();
            stopWatch.Start();
            for (int i = 1; i < 101; i++)
            {
                using (var session = new RuleSession(ruleAppDef))
                {
                    //session.Settings.MetricLogger = new MetricLogger("Server=tcp:ir-metrics.database.windows.net,1433;Initial Catalog=ir-metricstest;Persist Security Info=False;User ID=metrictest;Password=A5&5ySfjpnvp;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;");
                    //session.Settings.MetricLogger = new NoOpLogger();
                    session.Settings.MetricLogger = new MetricLogger(DatabaseConnectionString);
                    session.Settings.MetricServiceName = "Integration Tests";

                    var invoice = session.CreateEntity("Invoice");
                    invoice.ParseJson(entityState[i-1]);

                    session.ApplyRules();
                }
            }
            stopWatch.Stop();
            Console.WriteLine("Execution Time:" + stopWatch.ElapsedMilliseconds);
        }
    }
}