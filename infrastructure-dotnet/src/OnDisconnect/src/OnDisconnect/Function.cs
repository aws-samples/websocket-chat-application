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

namespace OnDisconnect;

public class Function
{
    private static string? StatusQueueUrl => Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.StatusQueueUrl);
    private static string? ConnectionsTableName => Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.ConnectionsTableName);

    private static DynamoDBContext _dynamoDbContext;
    private static AmazonSQSClient _sqsClient = new();
    static Function()
    {
        AWSSDKHandler.RegisterXRayForAllServices();
        
        if(string.IsNullOrEmpty(StatusQueueUrl))
        {
            throw new ArgumentException($"Missing ENV variable: {Constants.EnvironmentVariables.StatusQueueUrl}");
        }
        
        if (!string.IsNullOrEmpty(ConnectionsTableName))
        {
            AWSConfigsDynamoDB.Context.TypeMappings[typeof(Connection)] =
                new Amazon.Util.TypeMapping(typeof(Connection), ConnectionsTableName);
        }
        else
        {
            throw new ArgumentException($"Missing ENV variable: {Constants.EnvironmentVariables.ConnectionsTableName}");
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
        await LambdaBootstrapBuilder.Create(handler, new DefaultLambdaJsonSerializer(options => {
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
        Logger.LogInformation(new Dictionary<string, object>{{ "Lambda context", context }});
        var response = new APIGatewayProxyResponse { StatusCode = 200, Body = "OK" };

        Logger.LogInformation("Lambda has been invoked successfully.");
        
        var connectionId = apigProxyEvent.RequestContext.ConnectionId;

        try
        {
            // Get Connection item from DynamoDB to retrieve the userId
            var connectionItem = await _dynamoDbContext.LoadAsync<Connection>(connectionId);
            Logger.LogInformation($"Retrieved Connection Item: {connectionItem}");

            // Prepare status change event for broadcast
            var statusChangeEvent = new StatusChangeEvent(connectionItem.userId!, Status.OFFLINE, DateTime.Now);
            var statusChangeEventJson = JsonSerializer.Serialize(statusChangeEvent);
            Logger.LogInformation($"Putting status changed event in the SQS queue: {statusChangeEvent}");
            var sqsResults = await _sqsClient.SendMessageAsync(StatusQueueUrl, statusChangeEventJson);
            Logger.LogInformation($"SQS Queue send result: {sqsResults}");

            Logger.LogInformation($"Deleting record with id {connectionItem}");
            await _dynamoDbContext?.DeleteAsync(connectionItem)!;

            Metrics.AddMetric("closedConnection", 1, MetricUnit.Count);

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
}