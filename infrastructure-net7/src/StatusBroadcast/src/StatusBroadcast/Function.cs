using System.Text.Json;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.SQSEvents;
using Amazon.SQS;
using Amazon.XRay.Recorder.Handlers.AwsSdk;
using AWS.Lambda.Powertools.Logging;
using AWS.Lambda.Powertools.Metrics;
using AWS.Lambda.Powertools.Tracing;
using Shared;
using Shared.Models;

namespace StatusBroadcast;

public class Function
{
    public static string? ConnectionsTableName => Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.ConnectionsTableName);
    public static string? ApiGatewayEndpoint => Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.ApiGatewayEndpoint);

    private static readonly DynamoDBContext _dynamoDbContext;
    private static AmazonSQSClient _sqsClient = new();
    private static readonly WebsocketBroadcaster _websocketBroadcaster;
    
    static Function()
    {
        AWSSDKHandler.RegisterXRayForAllServices();
        
        if (!string.IsNullOrEmpty(ConnectionsTableName))
        {
            AWSConfigsDynamoDB.Context.TypeMappings[typeof(Connection)] =
                new Amazon.Util.TypeMapping(typeof(Connection), ConnectionsTableName);
        }
        
        var config = new DynamoDBContextConfig { Conversion = DynamoDBEntryConversion.V2 };
        _dynamoDbContext = new DynamoDBContext(new AmazonDynamoDBClient(), config);
        _websocketBroadcaster = new WebsocketBroadcaster(_dynamoDbContext);
    }
    
    /// <summary>
    /// The main entry point for the custom runtime.
    /// </summary>
    /// <param name="args"></param>
    private static async Task Main(string[] args)
    {
        Func<SQSEvent, ILambdaContext, Task> handler = FunctionHandler;
        await LambdaBootstrapBuilder.Create(handler, new DefaultLambdaJsonSerializer(options => {
                options.PropertyNameCaseInsensitive = true;
            }))
            .Build()
            .RunAsync();
    }
    
    [Logging(LogEvent = true, Service = "websocketMessagingService")]
    [Metrics(CaptureColdStart = true, Namespace = "websocket-chat")]
    [Tracing(CaptureMode = TracingCaptureMode.ResponseAndError, Namespace = "websocket-chat")]
    public static async Task FunctionHandler(SQSEvent sqsEvent, ILambdaContext context)
    {
        Logger.LogInformation(new Dictionary<string, object>{{ "Lambda context", context }});
        Logger.LogInformation("Lambda has been invoked successfully.");
        Logger.LogInformation($"APIGatewayEndpoint: {ApiGatewayEndpoint}");

        try
        {
            foreach (var eventRecord in sqsEvent.Records)
            {
                var statusChangeEvent = JsonSerializer.Deserialize<StatusChangeEvent>(eventRecord.Body);
                if (statusChangeEvent != null)
                {
                    await _websocketBroadcaster.Broadcast(JsonSerializer.Serialize(statusChangeEvent), ApiGatewayEndpoint!);
                }
            }
        }
        catch (Exception e)
        {
            Logger.LogInformation("Error happened while reading/broadcasting SQS messages!");
            Logger.LogError(e);
            Logger.LogError(e.StackTrace);
        }
    }
}