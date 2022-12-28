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

namespace OnMessage;

public class Function
{
    public static string? MessagesTableName => Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.MessagesTableName);
    public static string? ConnectionsTableName => Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.ConnectionsTableName);

    private static readonly DynamoDBContext _dynamoDbContext;
    private static AmazonSQSClient _sqsClient = new();
    private static readonly WebsocketBroadcaster _websocketBroadcaster;
    
    static Function()
    {
        AWSSDKHandler.RegisterXRayForAllServices();
        
        if (!string.IsNullOrEmpty(MessagesTableName) && !string.IsNullOrEmpty(ConnectionsTableName))
        {
            AWSConfigsDynamoDB.Context.TypeMappings[typeof(Message)] =
                new Amazon.Util.TypeMapping(typeof(Message), MessagesTableName);
            
            AWSConfigsDynamoDB.Context.TypeMappings[typeof(Connection)] =
                new Amazon.Util.TypeMapping(typeof(Connection), ConnectionsTableName);
        }//TODO: throw error if env variables are not present

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
        
        var apiGatewayEndpoint = $"{apigProxyEvent.RequestContext.DomainName}/{apigProxyEvent.RequestContext.Stage}";
        Logger.LogInformation($"APIGatewayEndpoint: {apiGatewayEndpoint}");

        try
        {
            var postObject = JsonSerializer.Deserialize<Payload>(apigProxyEvent.Body);
            if (postObject?.type == "Message")
            {
                var message = (Message)postObject;
                message.messageId = Guid.NewGuid().ToString();
                
                Logger.LogInformation($"Saving message {message}");
                await _dynamoDbContext.SaveAsync(message);

                Logger.LogInformation($"Broadcasting message {message}");
                await _websocketBroadcaster.Broadcast(message, apiGatewayEndpoint);
            }

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