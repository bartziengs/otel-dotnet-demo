// See https://aka.ms/new-console-template for more information
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Logic;
using Azure.ResourceManager.Logic.Models;
using LoggerWithOtelExporter.Models;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

var la = new ArmClient(new AzureCliCredential())
    .GetLogicWorkflowResource(
        LogicWorkflowResource.CreateResourceIdentifier(
            "ca7c1098-399a-4e10-bf3c-3f661713d40c",
            "oc-rg-altares-d-01",
            "oc-la-altares-idcheckgetter-d-we-01"
        )
    );

//Convert binary data definition to json
la = await la.GetAsync();
var def = la.Data.Definition.ToString();
//parse to Json with JsonNode
var json = JsonNode.Parse(def);

//Get trigger outputs
var triggerOutputs = json!["triggers"].AsObject().FirstOrDefault();


LogicWorkflowRunResource run = la.GetLogicWorkflowRun("08584616970295728835087197590CU225");

var actions = run.GetLogicWorkflowRunActions().Where(a => a.Data.Status != LogicWorkflowStatus.Skipped).ToList();

// initialize new http client and get 10th actions output
var httpClient = new HttpClient();
var actionOutput = await httpClient.GetAsync(actions[9].Data.InputsLink.Uri);

var jsonAction = actionOutput.Content.ReadAsStringAsync().Result;


var traceProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource(la.Data.Name)
    //todo verify later if this is needed
    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(
            serviceName: la.Data.Name,
            serviceInstanceId: run.Data.Name))
    .AddConsoleExporter()
    .AddOtlpExporter(otlpOptions =>
    {
        otlpOptions.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
    })
    .Build();

ActivitySource activitySource = new ActivitySource("logging");


////create LoggerFactory
var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddOpenTelemetry(logging =>
    {
        logging.IncludeFormattedMessage = true;
        logging.IncludeScopes = true;
        logging.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(
            serviceName: la.Data.Name,
            serviceInstanceId: run.Data.Name));
        logging.AddOtlpExporter(o =>
        {
            o.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
        });
        logging.AddConsoleExporter();
    });
});

LogModel logModel = new LogModel
{
    attempt = 1,
    BusinessObjectId = "123",
    category = "Data",
    entityType = "DDD",
    Response = "Success",
    ResponseCode = 200,
    source = "Altares",
    target = "Salesforce",
    Success = true,
};

var logger = loggerFactory.CreateLogger<Program>();
var tracer = traceProvider.GetTracer(la.Data.Name);

//var carrier = new KeyValuePair<string,string>() {"traceparent" : "00-a9c3b99a95cc045e573e163c3ac80a77-d99d251a8caecd06-01"};
// put carrier in key value pair
var carrier = new Dictionary<string, string> { 
    { "traceparent", "00-a9c3b99a95cc045e573e163c3ac80a77-d99d251a8caecd06-01" },
    { "tracestate", "d99d251a8caecd06" }
};

//extract activity context from carrier
PropagationContext? context = propagator.Extract(default, carrier, (c, k) => [c[k]]);
Propagators.DefaultTextMapPropagator.Inject(new PropagationContext(context, Baggage.Current), carrier, (c, k, v) => c[k] = v);

//var context = traceProvider.TextFormat.Extract<ActivityContext>(carrier, (c, k) => c.TraceId.ToString());

using (var activity = traceProvider.GetTracer(la.Data.Name).StartRootSpan(run.Data.Name, SpanKind.Consumer, startTime: run.Data.StartOn!.Value))
{
    //activity?.SetStatus(run.Data.Status!.Value == LogicWorkflowStatus.Succeeded ? Status.Ok : Status.Error);
    activity?.SetAttribute("actionName", actions[9].Data.Name);
    activity?.SetAttribute("actionStatus", actions[0].Data.Status.ToString());


    //initate new activityContext 
    ActivityContext parentContext = new ActivityContext(activity!.Context.TraceId, activity!.Context.SpanId, ActivityTraceFlags.Recorded);
    //start activity 
    using (var logActivity = traceProvider.GetTracer(la.Data.Name).StartActiveSpan("Log Message", parentContext: new SpanContext(parentContext), startTime: run.Data.StartOn!.Value))
    {
        using (logger.BeginScope(new Dictionary<string, object>
        {
            { "ts", run.Data.EndOn!.Value.DateTime.ToString("yyyy-MM-ddTHH:mm:ss.ffffff") },
            //add time in unix nanoseconds format 
        }))
        {
            logger.LogWarning("{logModel}", JsonSerializer.Serialize(logModel));
        }
        logActivity?.End(run.Data.EndOn!.Value);    
    }

    //start child span
    using (var childActivity = traceProvider.GetTracer(la.Data.Name).StartSpan(run.Data.Trigger.Name, SpanKind.Producer, parentContext: activity!.Context, startTime: run.Data.Trigger.StartOn!.Value))
    {
        //childActivity?.SetStatus(Status.Ok);
        childActivity?.SetAttribute("triggerStatus", run.Data.Trigger.Status.ToString());
        childActivity?.End(run.Data.Trigger.EndOn!.Value);
    }
    activity?.End(run.Data.EndOn!.Value);
}

Console.WriteLine("Actions");