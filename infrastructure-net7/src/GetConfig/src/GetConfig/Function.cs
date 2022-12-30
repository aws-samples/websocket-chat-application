using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Amazon.XRay.Recorder.Handlers.AwsSdk;
using AWS.Lambda.Powertools.Logging;
using AWS.Lambda.Powertools.Metrics;
using AWS.Lambda.Powertools.Tracing;
using Shared;
using Shared.Models;

namespace GetConfig;

public class Function
{
    private static readonly AmazonSimpleSystemsManagementClient _ssmClient;
    private static string? _cognitoSigninUrl;
    private static string? _websocketApiUrl;
    
    static Function()
    {
        AWSSDKHandler.RegisterXRayForAllServices();
        _ssmClient = new AmazonSimpleSystemsManagementClient();
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
        Logger.LogInformation("Lambda has been invoked successfully.");
        
        var apiGatewayEndpoint = $"{apigProxyEvent.RequestContext.DomainName}/{apigProxyEvent.RequestContext.Stage}";
        Logger.LogInformation($"APIGatewayEndpoint: {apiGatewayEndpoint}");

        try
        {
            // Retrieve and cache SSM Parameter value on first call to avoid repeated API requests
            if (_cognitoSigninUrl == null || _websocketApiUrl == null)
            {
                var ssmRequest = new GetParameterRequest()
                {
                    Name = Constants.SSMParameters.CognitoSigninUrl,
                    WithDecryption = true
                };
                var getParameterResponse = await _ssmClient.GetParameterAsync(ssmRequest);
                _cognitoSigninUrl = getParameterResponse.Parameter.Value;
                Logger.LogInformation($"Retrieved Cognito signin url parameter value: {_cognitoSigninUrl}");
                
                ssmRequest = new GetParameterRequest()
                {
                    Name = Constants.SSMParameters.WebsocketApiUrl,
                    WithDecryption = true
                };
                getParameterResponse = await _ssmClient.GetParameterAsync(ssmRequest);
                _websocketApiUrl = getParameterResponse.Parameter.Value;
                Logger.LogInformation($"Retrieved WebsocketAPI URL parameter value: {_websocketApiUrl}");
            }

            var config = new Config()
            {
                api_url = "/api",
                broadcast_url = _websocketApiUrl,
                login_url = _cognitoSigninUrl
            };
            return new APIGatewayProxyResponse { StatusCode = 200, Body = JsonSerializer.Serialize(config) };
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