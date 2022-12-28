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

namespace WebsocketAuthorizer;

public class Function
{
    public static string? CognitoUserPoolId => Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.CognitoUserPoolId);
    public static string? Region => Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.AwsRegion);
    
    private static readonly AmazonSimpleSystemsManagementClient _ssmClient;
    private static string? _cognitoClientId;
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
        Func<APIGatewayCustomAuthorizerRequest, ILambdaContext, Task<APIGatewayCustomAuthorizerResponse>> handler = FunctionHandler;
        await LambdaBootstrapBuilder.Create(handler, new SourceGeneratorLambdaJsonSerializer<CustomJsonSerializerContext>(options => {
                options.PropertyNameCaseInsensitive = true;
            }))
            .Build()
            .RunAsync();
    }
    
    [Logging(LogEvent = true, Service = "websocketMessagingService")]
    [Metrics(CaptureColdStart = true, Namespace = "websocket-chat")]
    [Tracing(CaptureMode = TracingCaptureMode.ResponseAndError, Namespace = "websocket-chat")]
    public static async Task<APIGatewayCustomAuthorizerResponse> FunctionHandler(APIGatewayCustomAuthorizerRequest apigProxyEvent, ILambdaContext context)
    {
        // Appended keys are added to all subsequent log entries in the current execution.
        // Call this method as early as possible in the Lambda handler.
        // Typically this is value would be passed into the function via the event.
        // Set the ClearState = true to force the removal of keys across invocations
        Logger.AppendKeys(new Dictionary<string, object>{{ "Lambda context", context }});
        Logger.AppendKeys(new Dictionary<string, object>{{ "ApiGateway event", apigProxyEvent }});
        Logger.LogInformation("Lambda has been invoked successfully.");
        
        var apiGatewayEndpoint = $"{apigProxyEvent.RequestContext.DomainName}/{apigProxyEvent.RequestContext.Stage}";
        Logger.LogInformation($"APIGatewayEndpoint: {apiGatewayEndpoint}");
        
        var token = apigProxyEvent.Headers["Cookie"].Split('=')[1];

        try
        {
            // Retrieve and cache SSM Parameter value on first call to avoid repeated API requests
            if (_cognitoClientId == null)
            {
                var ssmRequest = new GetParameterRequest()
                {
                    Name = Constants.SSMParameters.CognitoClientId,
                    WithDecryption = true
                };
                var getParameterResponse = await _ssmClient.GetParameterAsync(ssmRequest);
                _cognitoClientId = getParameterResponse.Parameter.Value;
                Logger.LogInformation($"Retrieved Cognito client id parameter value: {_cognitoClientId}");
            }
            
            var cognitoVerifier = new CognitoJwtVerifier(CognitoUserPoolId!, _cognitoClientId, Region!);
            
            var verifiedToken = cognitoVerifier.VerifyToken(token);
            if (verifiedToken != null)
            {
                Logger.LogInformation($"Token has been verified successfully.");    
                return GenerateAllow(verifiedToken.Claims.First(t=> t.Type == "cognito:username").Value, apigProxyEvent.MethodArn);
            }
            
            return GenerateDeny("default", apigProxyEvent.MethodArn);
        }
        catch (Exception e)
        {
            Logger.LogError(e.Message);

            return GenerateDeny("default", apigProxyEvent.MethodArn);
        }
    }

    private static APIGatewayCustomAuthorizerResponse GenerateAllow(string principalId, string resource)
    {
        return GeneratePolicy(principalId, "Allow", resource);
    }
    
    private static APIGatewayCustomAuthorizerResponse GenerateDeny(string principalId, string resource)
    {
        return GeneratePolicy(principalId, "Deny", resource);
    }

    private static APIGatewayCustomAuthorizerResponse GeneratePolicy(string principalId, string effect, string resource)
    {
        var policyDocument = new APIGatewayCustomAuthorizerPolicy()
        {
            Statement = new List<APIGatewayCustomAuthorizerPolicy.IAMPolicyStatement>()
            {
                new APIGatewayCustomAuthorizerPolicy.IAMPolicyStatement()
                {
                    Action = {"execute-api:Invoke"},
                    Effect = effect,
                    Resource = {resource}
                }
            }
        };
        var response = new APIGatewayCustomAuthorizerResponse()
        {
            PrincipalID = principalId,
            PolicyDocument = policyDocument
        };
        return response;
    }
}