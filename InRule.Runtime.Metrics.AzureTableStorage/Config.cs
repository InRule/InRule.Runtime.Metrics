using System.IO;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;

namespace InRule.Runtime.Metrics.AzureTableStorage
{
	public sealed class Config
	{
		public static Config Instance = GetConfig();

		private static Config GetConfig()
		{
			string configPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "AzureTableStoreKpiLogger.config.json");

			using (var fileStream = File.OpenRead(configPath))
			using (var reader = new StreamReader(fileStream, new UTF8Encoding(false)))
			{
				string json = reader.ReadToEndAsync().GetAwaiter().GetResult();
				return JsonConvert.DeserializeObject<Config>(json);
			}
		}

		[JsonProperty(PropertyName = "storageConnectionString")]
		public string StorageConnectionString { get; set; }

		[JsonProperty(PropertyName = "tableName")]
		public string TableName { get; set; }
	}
}
