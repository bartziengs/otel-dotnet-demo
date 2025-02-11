using Grpc.Net.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Common;
using OpenTelemetry.Logs;
using System.Text;

namespace OltpGrpcCaller
{
    public class SubmitOTLPWithGrpc
    {
        private readonly ILogger<SubmitOTLPWithGrpc> _logger;

        public SubmitOTLPWithGrpc(ILogger<SubmitOTLPWithGrpc> logger)
        {
            _logger = logger;
        }

        [Function(nameof(SubmitOTLPWithGrpc)]
        public IActionResult Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            //construct log body
            KeyValueList bodyValues = new();
            bodyValues.Values.AddRange(new List<KeyValue>() {
                new () {
                    Key = "ID",
                    Value = new AnyValue { StringValue = "value" }
                },
                new()
                {
                    Key = "Success",
                    Value = new AnyValue { BoolValue = true }
                },
                new()
                {
                    Key = "FavoriteNumber",
                    Value = new AnyValue { IntValue = 8 }
                }
            });

            //construct log record
            var logRecord = new LogRecord
            {
                TimeUnixNano = (ulong)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).Ticks * 100L,
                SeverityNumber = SeverityNumber.Info4,
                TraceId = Google.Protobuf.ByteString.CopyFrom(Encoding.UTF8.GetBytes("5B8EFFF798038103")),
                SeverityText = "Trace",
                EventName = "event",
                Body = new AnyValue { KvlistValue = bodyValues }
            };

            //construct scope logs
            ScopeLogs scopeLogs = new()
            {
                Scope = new InstrumentationScope()
                {
                    Name = "name",
                    Version = "version1",
                }
            };

            //add log record to scope logs
            scopeLogs.LogRecords.Add(logRecord);

            //construct resource logs
            ResourceLogs resourceLogs = new()
            {
                Resource = new OpenTelemetry.Resource.Resource
                {
                    Attributes = new KeyValue
                    {
                        Key = "key",
                        Value = new AnyValue { StringValue = "value" }
                    }
                }
            };

            //add scope logs to resource logs
            resourceLogs.ScopeLogs.Add(scopeLogs);

            //send logs to OTLP
            using var channel = GrpcChannel.ForAddress("http://localhost:4317");
            var client = new LogsService.LogsServiceClient(channel);

            ExportLogsServiceRequest request = new()
            {
                ResourceLogs = { resourceLogs }
            };

            ExportLogsServiceResponse res = client.Export(request);

            return new OkResult();
        }
    }
}
