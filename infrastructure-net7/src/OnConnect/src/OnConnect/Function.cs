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
    private static string? StatusQueueUrl => Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.StatusQueueUrl);
    private static string? ConnectionsTableName => Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.ConnectionsTableName);

    private static IDynamoDBContext? _dynamoDbContext;
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
        
        var authenticatedCustomerId = ((JsonElement)apigProxyEvent.RequestContext.Authorizer["customerId"]).GetString();
        Logger.LogInformation($"Authenticated customer id: {authenticatedCustomerId}");
        var connectionId = apigProxyEvent.RequestContext.ConnectionId;
        Logger.LogInformation($"Connection id: {connectionId}");

        // Prepare Connection object for insert
        var connection = new Connection(connectionId, authenticatedCustomerId);

        // Prepare status change event for broadcast
        var statusChangeEvent = new StatusChangeEvent(authenticatedCustomerId!, Status.ONLINE, DateTime.Now);
        var statusChangeEventJson = JsonSerializer.Serialize(statusChangeEvent);
        
        try
        {
            await SaveRecordInDynamo(connection);
            Metrics.AddMetric("newConnection", 1, MetricUnit.Count);

            Logger.LogInformation($"Putting status changed event in the SQS queue: {statusChangeEvent}");
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
    //[Tracing(SegmentName = "DynamoDB")]
    private static async Task SaveRecordInDynamo(Connection connection)
    {
        try
        {
            Logger.LogInformation($"Saving connection item with id {connection}");
            await _dynamoDbContext?.SaveAsync(connection)!;
        }
        catch (AmazonDynamoDBException e)
        {
            Logger.LogCritical(e.Message);
            throw;
        }
    }
}