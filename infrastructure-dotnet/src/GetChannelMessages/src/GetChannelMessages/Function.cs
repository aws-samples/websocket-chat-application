using System.Text.Json;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
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

namespace GetChannelMessages;

public class Function
{
    private static string? MessagesTableName => Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.MessagesTableName);
    private static readonly DynamoDBContext _dynamoDbContext;
    
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

        var channelId = apigProxyEvent.PathParameters["id"];
        Logger.LogInformation($"Channel id: {channelId}");
        
        // Trace Fluent API
        Tracing.WithSubsegment("ChannelRequest",
            subsegment =>
            {
                subsegment.AddAnnotation("ChannelId", channelId);
            });
        
        try
        {
            var messages = await _dynamoDbContext.ScanAsync<Message>(new []{new ScanCondition("channelId", ScanOperator.Equal, channelId)}).GetRemainingAsync();
            Logger.LogInformation($"Retrieved messages for channel {channelId}");
            Logger.LogInformation(messages);
            
            response.Body = JsonSerializer.Serialize(messages.ToArray());
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