//////////////////////////////////////////////////////////////////////
// PARAMETERS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Local");
var nugetSourceFeedUrl = Argument("nugetSourceFeedUrl", "");
var versionPrefix = Argument("versionPrefix", "5.4.0");
var versionSuffix = Argument("versionSuffix", "0");
var nugetPushFeedUrl = Argument("nugetPushFeedUrl", "");
var nugetPushApiKey = Argument("nugetPushApiKey", "");

//////////////////////////////////////////////////////////////////////
// GLOBALS
//////////////////////////////////////////////////////////////////////

const string solution = "./InRule.Runtime.Metrics.sln";
const string nuGetOrgUrl = "https://api.nuget.org/v3/index.json";
const string nugetPackagesFolder = "./NuGetPackages";
const string releaseConfiguration = "Release";
const string versionPrefixProperty = "VersionPrefix";

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Clean")
  .Does(() =>
{
  DotNetCoreClean(solution);

  try
  {
    CleanDirectory(nugetPackagesFolder);
  }
  catch { }
});

Task("Restore .NET Dependencies")
  .Does(() =>
{
  ICollection<string> sources;

  if (HasArgument("nugetSourceFeedUrl"))
  {
    Warning("{0} added as an additional NuGet feed.", nugetSourceFeedUrl);
    sources = new[] { nugetSourceFeedUrl, nuGetOrgUrl };
  }
  else
  {
    Warning("No additional NuGet feed specified.");
    sources = new[] { nuGetOrgUrl };
  }

  DotNetCoreRestore(solution, new DotNetCoreRestoreSettings { Sources = sources });
});

Task("Build and Publish Metrics Adapter Libraries")
  .Does(() =>
{
  var settings = new DotNetCorePublishSettings
  {
    Configuration = releaseConfiguration,
    VersionSuffix = versionSuffix,
    MSBuildSettings = new DotNetCoreMSBuildSettings().WithProperty(versionPrefixProperty, versionPrefix),
  };

  DotNetCorePublish(solution, settings);
});

Task("Test SQL Adapter")
  .Does(() =>
{
  DotNetCoreTest("./InRule.Runtime.Metrics.SqlServer.IntegrationTests/InRule.Runtime.Metrics.SqlServer.IntegrationTests.csproj");
});

Task("Create Metrics Adapter NuGet Packages")
  .Does(() =>
{
  var settings = new DotNetCorePackSettings
  {
    NoBuild = true,
    Configuration = releaseConfiguration,
    OutputDirectory = nugetPackagesFolder,
    VersionSuffix = versionSuffix,
    MSBuildSettings = new DotNetCoreMSBuildSettings().WithProperty(versionPrefixProperty, versionPrefix),
  };

  DotNetCorePack(solution, settings);
});

Task("Publish to NuGet Feed")
  .Does(() =>
{
  if (!HasArgument("nugetPushFeedUrl"))
  {
    Error("nugetPushFeedUrl argument is required.");
  }

  if (!HasArgument("nugetPushApiKey"))
  {
    Error("nugetPushApiKey argument is required.");
  }

  var settings = new DotNetCoreNuGetPushSettings
  {
    Source = nugetPushFeedUrl,
    ApiKey = nugetPushApiKey,
    WorkingDirectory = nugetPackagesFolder,
  };

  DotNetCoreNuGetPush("*.nupkg", settings);
});

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Local")
    .IsDependentOn("Clean")
    .IsDependentOn("Restore .NET Dependencies")
    .IsDependentOn("Build and Publish Metrics Adapter Libraries")
    .IsDependentOn("Test SQL Adapter")
    .IsDependentOn("Create Metrics Adapter NuGet Packages");

Task("Publish")
    .IsDependentOn("Clean")
    .IsDependentOn("Restore .NET Dependencies")
    .IsDependentOn("Build and Publish Metrics Adapter Libraries")
    .IsDependentOn("Test SQL Adapter")
    .IsDependentOn("Create Metrics Adapter NuGet Packages")
    .IsDependentOn("Publish to NuGet Feed");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);