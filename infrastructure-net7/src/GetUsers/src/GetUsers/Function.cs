using Amazon;
using Amazon.CognitoIdentity;
using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
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

namespace GetUsers;

public class Function
{
    public static string? ConnectionsTableName => Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.ConnectionsTableName);
    public static string? CognitoUserPoolId => Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.CognitoUserPoolId);

    private static readonly DynamoDBContext _dynamoDbContext;

    static Function()
    {
        AWSSDKHandler.RegisterXRayForAllServices();
        
        if (!string.IsNullOrEmpty(ConnectionsTableName))
        {
            AWSConfigsDynamoDB.Context.TypeMappings[typeof(Connection)] =
                new Amazon.Util.TypeMapping(typeof(Connection), ConnectionsTableName);
        }//TODO: throw error if env variables are not present

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
        
        try
        {
            Logger.LogInformation("Retrieving active connections...");
            var connectionData = await _dynamoDbContext.ScanAsync<Connection>(Array.Empty<ScanCondition>()).GetRemainingAsync();

            // Get all Cognito users
            AmazonCognitoIdentityProviderClient cognitoClient = new AmazonCognitoIdentityProviderClient();
            var users = await cognitoClient.ListUsersAsync(new ListUsersRequest()
            {
                UserPoolId = CognitoUserPoolId
            });
            
            // Merge list into response format
            //TODO: implement the rest of the method
            
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