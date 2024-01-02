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
        await LambdaBootstrapBuilder.Create(handler, new DefaultLambdaJsonSerializer(options => {
                options.PropertyNameCaseInsensitive = true;
            }))
            .Build()
            .RunAsync();
    }
    
    [Logging(Service = "websocketMessagingService")]
    //[Metrics(CaptureColdStart = true, Namespace = "websocket-chat")]
    //[Tracing(CaptureMode = TracingCaptureMode.ResponseAndError, Namespace = "websocket-chat")]
    public static async Task<APIGatewayCustomAuthorizerResponse> FunctionHandler(APIGatewayCustomAuthorizerRequest apigProxyEvent, ILambdaContext context)
    {
        Logger.LogInformation(new Dictionary<string, object>{{ "Lambda context", context }});
        Logger.LogInformation(new Dictionary<string, object>{{ "ApiGateway event", apigProxyEvent }});
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
                var policyResult = GenerateAllow(verifiedToken.Claims.First(t=> t.Type == "cognito:username").Value, apigProxyEvent.MethodArn);
                Logger.LogInformation(policyResult);
                return policyResult;
            }
            
            Logger.LogInformation("Authorization failed. Returning Deny policy.");
            return GenerateDeny("default", apigProxyEvent.MethodArn);
        }
        catch (Exception e)
        {
            Logger.LogError(e.Message);

            Logger.LogInformation("Authorization failed. Returning Deny policy.");
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
            Version = "2012-10-17",
            Statement = new List<APIGatewayCustomAuthorizerPolicy.IAMPolicyStatement>()
            {
                new APIGatewayCustomAuthorizerPolicy.IAMPolicyStatement()
                {
                    Action = new HashSet<string>(){"execute-api:Invoke"},
                    Effect = effect,
                    Resource = new HashSet<string>(){resource}
                }
            }
        };
        var response = new APIGatewayCustomAuthorizerResponse()
        {
            PrincipalID = principalId,
            PolicyDocument = policyDocument,
            Context = new APIGatewayCustomAuthorizerContextOutput()
            {
                {"customerId", principalId}
            }
        };
        return response;
    }
}