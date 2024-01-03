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

namespace PostChannels;

public class Function
{
    private static string? ChannelsTableName => Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.ChannelsTableName);
    private static readonly DynamoDBContext _dynamoDbContext;

    static Function()
    {
        AWSSDKHandler.RegisterXRayForAllServices();
        
        if (!string.IsNullOrEmpty(ChannelsTableName))
        {
            AWSConfigsDynamoDB.Context.TypeMappings[typeof(Channel)] =
                new Amazon.Util.TypeMapping(typeof(Channel), ChannelsTableName);
        }
        else
        {
            throw new ArgumentException($"Missing ENV variable: {Constants.EnvironmentVariables.ChannelsTableName}");
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
        Logger.LogInformation(new Dictionary<string, object>{{ "Lambda context", context }});
        var response = new APIGatewayProxyResponse { StatusCode = 200, Body = "" };

        Logger.LogInformation("Lambda has been invoked successfully.");
        var channel = JsonSerializer.Deserialize<Channel>(apigProxyEvent.Body);

        try
        {
            if (channel == null)
                throw new Exception(
                    "Could not deserialize Channel object! Check ApiGW Proxy event for payload details.");
            
            await _dynamoDbContext.SaveAsync(channel);
            Logger.LogInformation("Channel saved to DynamoDB");
 
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