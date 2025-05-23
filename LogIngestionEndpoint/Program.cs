// See https://aka.ms/new-console-template for more information
using Azure.Core;
using Azure.Identity;
using Azure.Monitor.Ingestion;
using System.Text.Json;


// Initialize variables
var endpoint = new Uri("https://bz-dcr-poc-ingestion-01-bvy2-westeurope.logs.z1.ingest.monitor.azure.com");
var ruleId = "dcr-4a698aae277f41c29d4763a1da9aeb39";
var streamName = "Custom-POCTable_CL";

// Create credential and client
var credential = new AzureCliCredential();
LogsIngestionClient client = new(endpoint, credential);

DateTimeOffset currentTime = DateTimeOffset.UtcNow;
object content = new [] {
    new
    {
      Time = currentTime,
      Computer = "Computer1",
      AdditionalContext = new
      {
        InstanceName = "user1",
        TimeZone = "Pacific Time",
        Level = 4,
        CounterName = "AppMetric1",
        CounterValue = 15.3
      }
    },
    new
    {
      Time = currentTime,
      Computer = "Computer2",
      AdditionalContext = new
      {
        InstanceName = "user2",
        TimeZone = "Central Time",
        Level = 3,
        CounterName = "AppMetric1",
        CounterValue = 23.5
      }
    }
};

// Use BinaryData to serialize instances of an anonymous type into JSON
BinaryData data = BinaryData.FromObjectAsJson(content);
string s = JsonSerializer.Serialize(content);

// Upload logs
try
{
    var req = RequestContent.Create(content);
    //RequestContent.Create(data));
    var response = await client.UploadAsync(ruleId, streamName, req).ConfigureAwait(false);
    if (response.IsError)
    {
        throw new Exception(response.ToString());
    }

    Console.WriteLine("Log upload completed using content upload");
}
catch (Exception ex)
{
    Console.WriteLine("Upload failed with Exception: " + ex.Message);
}