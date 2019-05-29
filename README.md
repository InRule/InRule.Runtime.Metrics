# InRule.Runtime.Metrics

InRule Metrics provides the ability to log field and rule values directly from the engine to a data store of your choosing. This information is valuable to see how rules are performing over time. They can be thought of as the Key Performance Indicators (KPI’s) that want to be tracked during rule execution. 
 
How it works
In irAuthor, you can flag fields and select rules as a Metric. During execution, the engine will collect the field or rule name as well as the value of each. The engine will then emit the metrics to a Metric Logger (discussed below). Metrics are emitted on a per entity basis, meaning all the fields and rules will be emitted in the context of it’s entity. For example, if the Product and Total field are flagged as metrics on the Invoice entity, the rule engine will emit an Invoice metric, with the name and values for Product and Total.
 
To handle the multitude of options for desired logging locations, InRule has implemented an adaptor based model. An adaptor is a .NET assembly that is available to the rule engine that implements the IMetricLogger interface. When this assembly exists, the engine will call out to the required methods in the assembly to perform the actual logging. This provides customers the ability to write metrics to any location that is required in their implementation.
 
At the time of this writing, there are 3 adaptors that are available.
- Azure Table Storage 
- SQL Server
- CSV (primarily for demo purposes)
