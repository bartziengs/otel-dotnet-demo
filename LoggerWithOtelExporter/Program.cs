// See https://aka.ms/new-console-template for more information
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Logic;
using Azure.ResourceManager.Logic.Models;
using LoggerWithOtelExporter.Models;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Text.Json;

Console.WriteLine("Hello, World!");

const string logSource = "logging";
bool isRoot = true;

string laName = "oc-la-salesforce-idcheckproc-d-we-01";
//string laName = "oc-la-altares-idcheckgetter-d-we-01";
string rgName = "oc-rg-salesforce-d-01";
//string rgName = "oc-rg-altares-d-01";
string runId = "08584611970329106263704584196CU139";

var la = new ArmClient(new AzureCliCredential())
    .GetLogicWorkflowResource(
        LogicWorkflowResource.CreateResourceIdentifier(
            "ca7c1098-399a-4e10-bf3c-3f661713d40c",
            rgName,
            laName
        )
    ).Get().Value;


LogicWorkflowRunResource run = la.GetLogicWorkflowRun(runId);
//get actions
var actions = run.GetLogicWorkflowRunActions().Where(a => a.Data.Status != LogicWorkflowStatus.Skipped).Take(5).ToList();

var traceProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource(logSource)
    .SetSampler(new AlwaysOnSampler())
    .ConfigureResource(r => r
        .AddService(
            serviceName: laName,
            serviceInstanceId: runId,
            serviceVersion: "1.0.0"))
    .AddConsoleExporter()
    .AddOtlpExporter(otlpOptions =>
    {
        otlpOptions.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
    })
    .Build();



SpanContext activityContext = new(ActivityContext.Parse("00-69551743cbc24ec9a23cc94a70de274e-e916c59e932597cd-00", string.Empty));

//create LoggerFactory
var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddOpenTelemetry(logging =>
    {
        logging.IncludeFormattedMessage = true;
        logging.IncludeScopes = true;
        logging.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(
            serviceName: laName,
            serviceInstanceId: runId,
            serviceVersion: "1.0.0"));
        logging.AddOtlpExporter(o => o.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf);
        logging.AddConsoleExporter();
    });
});

var logger = loggerFactory.CreateLogger<Program>();


LogModel logModel = new LogModel
{
    attempt = 1,
    BusinessObjectId = "XGRE2000",
    category = "Data",
    entityType = "DDD",
    Response = "Success",
    ResponseCode = 200,
    source = "Afas",
    target = "Salesforce",
    Success = false,
};


ActivitySource activitySource = new ActivitySource(logSource);
var a = activitySource.StartActivity("test", ActivityKind.Internal, activityContext, null, null, DateTimeOffset.Now);

Activity.TraceIdGenerator = () => activityContext.TraceId;
Tracer logicAppTracer = traceProvider.GetTracer(logSource);

using (var laSpan = logicAppTracer.StartRootSpan(la.Data.Name, SpanKind.Server, startTime: run.Data.StartOn!.Value))
{
    Tracer.WithSpan(laSpan);
    laSpan.SetAttribute("runId", runId);
    logger.LogInformation("{logModel}", JsonSerializer.Serialize(logModel));

    actions.ForEach(a =>
    {
        using (var actionSpan = logicAppTracer.StartActiveSpan(a.Data.Name, SpanKind.Client, parentContext: laSpan.Context, startTime: a.Data.StartOn!.Value))
        {
            actionSpan.SetStatus(a.Data.Status == LogicWorkflowStatus.Succeeded ? Status.Ok : Status.Error);
            actionSpan.End(a.Data.EndOn!.Value);
        }
    });
    laSpan.End(run.Data.EndOn!.Value);
}


loggerFactory.Dispose();
traceProvider.Dispose();

var b = 8;