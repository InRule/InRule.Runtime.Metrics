InRule® Metric logger for the InRule Rule Engine that can log to Microsoft SQL Server.

**Configuring via code:**
```c#
using (var session = new RuleSession(ruleApplicationDef))
{
    session.Settings.MetricLogger = new MetricLogger(DatabaseConnectionString);
    session.Settings.MetricLoggerName = "Development"; //Optional name
    
    session.CreateEntity(e1Def.Name);
    session.ApplyRules();
}
```
**Configuring via custom config section:**
```xml
<configuration>
  <configSections>
    <section name="inrule.runtime" type="InRule.Runtime.Configuration.RuntimeConfigSectionHandler, InRule.Runtime" />
  </configSections>
  
  <!-- Other sections ommitted for brevity -->
  
  <appSettings>
    <!-- Connection String to Metrics storage location -->
    <add key="inrule:runtime:metrics:sqlServer:connectionString" value="" />  
  </appSettings>  
    
  <inrule.runtime>
    <ruleEngine 
                metricLoggerAssemblyName="InRule.Runtime.Metrics.SqlServer" 
                metricLoggerTypeName="InRule.Runtime.Metrics.SqlServer.MetricLogger" 
                metricLoggerServiceName="Development"/> <!-- Optional Name -->
  </inrule.runtime>
</configuration>
```

**Configuring via appSettings:**
```xml
<configuration>
  
  <!-- Other sections ommitted for brevity -->
  
  <appSettings>
    <add key="inrule:runtime:ruleEngine:metricLoggerAssemblyName" value="InRule.Runtime.Metrics.SqlServer" />    
    <add key="inrule:runtime:ruleEngine:metricLoggerTypeName" value="InRule.Runtime.Metrics.SqlServer.MetricLogger" />
    <!-- Optional Name -->
    <add key="inrule:runtime:ruleEngine:metricLoggerServiceName" value="Development" />
    <!-- Connection String to Metrics storage location -->
    <add key="inrule:runtime:metrics:sqlServer:connectionString" value="" />  
  </appSettings>
</configuration>
```
