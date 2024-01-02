using System.Text.Json;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.XRay.Recorder.Handlers.AwsSdk;
using AWS.Lambda.Powertools.Logging;
using AWS.Lambda.Powertools.Metrics;
using AWS.Lambda.Powertools.Tracing;
using Shared;
using Shared.Models;

namespace OnMessage;

public class Function
{
    private static string? MessagesTableName => Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.MessagesTableName);
    private static string? ConnectionsTableName => Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.ConnectionsTableName);

    private static readonly DynamoDBContext _dynamoDbContext;
    private static readonly WebsocketBroadcaster _websocketBroadcaster;
    
    static Function()
    {
        AWSSDKHandler.RegisterXRayForAllServices();
        
        if (!string.IsNullOrEmpty(MessagesTableName))
        {
            AWSConfigsDynamoDB.Context.TypeMappings[typeof(Message)] =
                new Amazon.Util.TypeMapping(typeof(Message), MessagesTableName);
        }
        else
        {
            throw new ArgumentException($"Missing ENV variable: {Constants.EnvironmentVariables.MessagesTableName}");
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
        _websocketBroadcaster = new WebsocketBroadcaster(_dynamoDbContext);
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
        
        var apiGatewayEndpoint = $"https://{apigProxyEvent.RequestContext.DomainName}/{apigProxyEvent.RequestContext.Stage}";
        Logger.LogInformation($"APIGatewayEndpoint: {apiGatewayEndpoint}");

        try
        {
            //TODO: this is messy, find a cleaner solution to deserialize payload content
            var postObject = JsonSerializer.Deserialize<WebsocketPayload>(apigProxyEvent.Body);
            var payloadString = ((JsonElement)postObject.data).ToString();
            var message = JsonSerializer.Deserialize<Message>(payloadString);

            if (message != null)
            {
                Logger.LogInformation($"Received Message...");
                Logger.LogInformation(message);
                message.messageId = Guid.NewGuid().ToString();
                
                Logger.LogInformation($"Saving message {message}");
                await _dynamoDbContext.SaveAsync(message);

                Logger.LogInformation($"Broadcasting message {message}");
                await _websocketBroadcaster.Broadcast(JsonSerializer.Serialize(message), apiGatewayEndpoint);
            }
            else
            {
                throw new Exception("Invalid payload - cannot deserialize to Message object!");
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