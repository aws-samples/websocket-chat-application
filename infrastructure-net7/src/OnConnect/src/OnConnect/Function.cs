using System.Text.Json;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.SQS;
using Amazon.XRay.Recorder.Handlers.AwsSdk;
using AWS.Lambda.Powertools.Logging;
using AWS.Lambda.Powertools.Metrics;
using AWS.Lambda.Powertools.Tracing;
using Shared;
using Shared.Models;

namespace OnConnect;

public class Function
{
    public static string? StatusQueueUrl => Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.StatusQueueUrl);
    public static string? ConnectionsTableName => Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.ConnectionsTableName);

    private static IDynamoDBContext? _dynamoDbContext;
    private static AmazonSQSClient _sqsClient = new();
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
    }
    
    /// <summary>
    /// The main entry point for the custom runtime.
    /// </summary>
    /// <param name="args"></param>
    private static async Task Main(string[] args)
    {
        Func<APIGatewayProxyRequest, ILambdaContext, Task<APIGatewayProxyResponse>> handler = FunctionHandler;
        await LambdaBootstrapBuilder.Create(handler, new SourceGeneratorLambdaJsonSerializer<CustomJsonSerializerContext>(options => {
                options.PropertyNameCaseInsensitive = true;
            }))
            .Build()
            .RunAsync();
    }
    
    [Logging(LogEvent = true, Service = "websocketMessagingService")]
    [Metrics(CaptureColdStart = true, Namespace = "websocket-chat")]
    [Tracing(CaptureMode = TracingCaptureMode.ResponseAndError, Namespace = "websocket-chat")]
    public static async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest apigProxyEvent, ILambdaContext context)
    {
        // Appended keys are added to all subsequent log entries in the current execution.
        // Call this method as early as possible in the Lambda handler.
        // Typically this is value would be passed into the function via the event.
        // Set the ClearState = true to force the removal of keys across invocations
        Logger.AppendKeys(new Dictionary<string, object>{{ "Lambda context", context }});
        Logger.AppendKeys(new Dictionary<string, object>{{ "ApiGateway event", apigProxyEvent }});
        var response = new APIGatewayProxyResponse { StatusCode = 200, Body = "OK" };

        Logger.LogInformation("Lambda has been invoked successfully.");
        
        var authenticatedCustomerId = apigProxyEvent.RequestContext.Authorizer?.Claims["customerId"];
        var connectionId = apigProxyEvent.RequestContext.ConnectionId;

        // Prepare Connection object for insert
        var connection = new Connection(connectionId, authenticatedCustomerId);

        // Prepare status change event for broadcast
        var statusChangeEvent = new StatusChangeEvent(authenticatedCustomerId!, Status.ONLINE, DateTime.Now);
        var statusChangeEventJson = JsonSerializer.Serialize(statusChangeEvent);
        
        try
        {
            await SaveRecordInDynamo(connection);
            Metrics.AddMetric("newConnection", 1, MetricUnit.Count);

            Logger.LogInformation($"Putting status changed event in the SQS queue...");
            Logger.LogInformation(statusChangeEvent);
            
            var sqsResults = await _sqsClient.SendMessageAsync(StatusQueueUrl, statusChangeEventJson);
            Logger.LogInformation("SQS Queue send result:");
            Logger.LogInformation(sqsResults);
            
            return response;
        }
        catch (Exception e)
        {
            Logger.LogError(e.Message);
            
            return new APIGatewayProxyResponse
            {
                Body = e.Message,
                StatusCode = 500,
                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
            };
        }
    }
    
    /// <summary>
    /// Saves the connection record in DynamoDB
    /// </summary>
    /// <param name="connection">Instance of Connection</param>
    /// <returns>A Task that can be used to poll or wait for results, or both.</returns>
    [Tracing(SegmentName = "DynamoDB")]
    private static async Task SaveRecordInDynamo(Connection connection)
    {
        try
        {
            Logger.LogInformation($"Saving record with id {connection}");
            await _dynamoDbContext?.SaveAsync(connection)!;
        }
        catch (AmazonDynamoDBException e)
        {
            Logger.LogCritical(e.Message);
            throw;
        }
    }
}