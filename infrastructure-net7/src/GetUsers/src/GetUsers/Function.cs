using System.Text.Json;
using Amazon;
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
    private static string? ConnectionsTableName => Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.ConnectionsTableName);
    private static string? CognitoUserPoolId => Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.CognitoUserPoolId);

    private static readonly DynamoDBContext _dynamoDbContext;

    static Function()
    {
        AWSSDKHandler.RegisterXRayForAllServices();
        
        if (!string.IsNullOrEmpty(ConnectionsTableName))
        {
            AWSConfigsDynamoDB.Context.TypeMappings[typeof(Connection)] =
                new Amazon.Util.TypeMapping(typeof(Connection), ConnectionsTableName);
        } else
        {
            throw new ArgumentException($"Missing ENV variable: {Constants.EnvironmentVariables.ConnectionsTableName}");
        }

        if (string.IsNullOrEmpty(CognitoUserPoolId))
        {
            throw new ArgumentException($"Missing ENV variable: {Constants.EnvironmentVariables.CognitoUserPoolId}");
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
            
            // Merge list of users into response format
            var userList = new List<User>();
            foreach (var user in users.Users)
            {
                var userIsConnected = connectionData.FirstOrDefault(c => c.userId == user.Username) != null;
                userList.Add(new User()
                {
                    username = user.Username,
                    status = userIsConnected ? Status.ONLINE : Status.OFFLINE
                });
            }
            Logger.LogInformation("Finished compiling user list");
            Logger.LogInformation(users);

            response.Body = JsonSerializer.Serialize(userList.ToArray());
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