using System.Collections.Generic;
using Amazon.CDK;
using Amazon.CDK.AWS.APIGateway;
using Amazon.CDK.AWS.Apigatewayv2;
using Amazon.CDK.AWS.Cognito;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.IAM;
using Constructs;
using Microsoft.Extensions.Logging;
using IResource = Amazon.CDK.AWS.APIGateway.IResource;

namespace Infrastructure.Stacks
{
    public class RestApiStackProps : StackProps
    {
        public Table MessagesTable { get; set; }
        public Table ChannelsTable { get; set; }
        public Table ConnectionsTable { get; set; }
        public string CognitoUserPoolId { get; set; }
        public WebSocketApi WebSocketApi { get; set; }
        public LogLevel LogLevel { get; set; }
    }
    public class RestApiStack : Stack
    {
        public string ApiGatewayEndpoint { get; set; }
        public RestApi RestApi { get; set; }
        internal RestApiStack(Construct scope, string id, RestApiStackProps props = null) : base(scope, id, props)
        {
            /* ================================
            API Schema
            -----------
            [GET]    /config
            [GET]    /users
            [GET]    /channels
            [GET]    /channels/{ID}
            [POST]   /channels/
            [GET]    /channels/{ID}/messages
            ==================================== */
            
            var defaultLambdaEnvironment = new Dictionary<string, string>()
            {
                {"CONNECTIONS_TABLE_NAME", props?.ConnectionsTable.TableName},
                {"MESSAGES_TABLE_NAME", props?.MessagesTable.TableName},
                {"CHANNELS_TABLE_NAME", props?.ChannelsTable.TableName},
                {"COGNITO_USER_POOL_ID", props?.CognitoUserPoolId},
                {"WEBSOCKET_API_URL", $"https://{props?.WebSocketApi.ApiEndpoint!}/wss"},
                {"POWERTOOLS_LOG_LEVEL", props?.LogLevel.ToString()}
            };
            
            var getUsersHandler = new CustomRuntimeFunction(this, "GetUsersHandler",
                "./src/","./GetUsers/src/GetUsers", 
                "bootstrap::GetUsers.Function::FunctionHandler", defaultLambdaEnvironment);
            props?.ConnectionsTable.GrantReadData(getUsersHandler);
            getUsersHandler.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps()
            {
                Effect = Effect.ALLOW,
                Actions = new[]{ "cognito-idp:ListUsers" },
                Resources = new []{$"arn:aws:cognito-idp:{Stack.Of(this).Region}:{Stack.Of(this).Account}:userpool/{props?.CognitoUserPoolId!}"}
            }));
            
            var getConfigHandler = new CustomRuntimeFunction(this, "GetConfigHandler",
                "./src/","./GetConfig/src/GetConfig", 
                "bootstrap::GetConfig.Function::FunctionHandler", defaultLambdaEnvironment);
            getConfigHandler.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps()
            {
                Effect = Effect.ALLOW,
                Actions = new[]{ 
                    "ssm:GetParameter",
                    "ssm:GetParameters",
                    "ssm:GetParametersByPath" 
                },
                Resources = new []
                {
                    $"arn:aws:ssm:{Stack.Of(this).Region}:{Stack.Of(this).Account}:parameter/prod/cognito/signinurl",
                    $"arn:aws:ssm:{Stack.Of(this).Region}:{Stack.Of(this).Account}:parameter/prod/websocket/url",
                }
            }));
            
            var getChannelsHandler = new CustomRuntimeFunction(this, "GetChannelsHandler",
                "./src/","./GetChannels/src/GetChannels", 
                "bootstrap::GetChannels.Function::FunctionHandler", defaultLambdaEnvironment);
            
            var postChannelsHandler = new CustomRuntimeFunction(this, "PostChannelsHandler",
                "./src/","./PostChannels/src/PostChannels", 
                "bootstrap::PostChannels.Function::FunctionHandler", defaultLambdaEnvironment);
            
            var getChannelHandler = new CustomRuntimeFunction(this, "GetChannelHandler",
                "./src/","./GetChannel/src/GetChannel", 
                "bootstrap::GetChannel.Function::FunctionHandler", defaultLambdaEnvironment);
            
            var getChannelMessagesHandler = new CustomRuntimeFunction(this, "GetChannelMessagesHandler",
                "./src/","./GetChannelMessages/src/GetChannelMessages", 
                "bootstrap::GetChannelMessages.Function::FunctionHandler", defaultLambdaEnvironment);

            // Grant the Lambda functions read/write access to the DynamoDB tables
            props?.ChannelsTable.GrantReadWriteData(getChannelsHandler);
            props?.ChannelsTable.GrantReadData(getChannelsHandler);
            props?.ChannelsTable.GrantReadWriteData(postChannelsHandler);
            props?.ChannelsTable.GrantReadData(getChannelHandler);
            props?.MessagesTable.GrantReadData(getChannelMessagesHandler);
            
            // Integrate the Lambda functions with the API Gateway resource
            var getConfigIntegration = new LambdaIntegration(getConfigHandler);
            var getUsersIntegration = new LambdaIntegration(getUsersHandler);
            var getChannelsIntegration = new LambdaIntegration(getChannelsHandler);
            var postChannelsIntegration = new LambdaIntegration(postChannelsHandler);
            var getChannelIntegration = new LambdaIntegration(getChannelHandler);
            var getChannelMessagesIntegration = new LambdaIntegration(getChannelMessagesHandler);

            this.RestApi = new RestApi(this, "ServerlessChatRestApi", new RestApiProps()
            {
                RestApiName = "Serverless Chat REST API"
            });

            this.ApiGatewayEndpoint = this.RestApi.Url;
            
            // Retrieving Cognito Userpool from existing resource - resolving circular stack dependency
            var userPool = UserPool.FromUserPoolId(this, "UserPool", props.CognitoUserPoolId);
            var auth = new CognitoUserPoolsAuthorizer(this, "websocketChatUsersAuthorizer",
                new CognitoUserPoolsAuthorizerProps()
                {
                    CognitoUserPools = new []{ userPool }
                }
            );
            
            var authMethodOptions = new MethodOptions()
            {
                Authorizer = auth,
                AuthorizationType = AuthorizationType.COGNITO
            };

            var api = this.RestApi.Root.AddResource("api");
            
            var config = api.AddResource("config");
            /* [GET]  /config - Retrieve all users with online/offline status */
            config.AddMethod("GET", getConfigIntegration);

            var users = api.AddResource("users");
            /* [GET]  /users - Retrieve all users with online/offline status */
            users.AddMethod("GET", getUsersIntegration, authMethodOptions);

            var channels = api.AddResource("channels");
            /* [GET]  /channels - Retrieve all channels with participant details */
            channels.AddMethod("GET", getChannelsIntegration, authMethodOptions);
            /* [POST] /channels - Creates a new channel */
            channels.AddMethod("POST", postChannelsIntegration, authMethodOptions);
            /* [ANY] /channels/{id} - retrieves channel with specific ID */
            var channelId = channels.AddResource("{id}");
            channelId.AddMethod("GET", getChannelIntegration, authMethodOptions);

            var messages = channelId.AddResource("messages");
            /* [GET]  /channels/{ID}/messages - Retrieve top 100 messages for a specific channel */
            messages.AddMethod("GET", getChannelMessagesIntegration, authMethodOptions);
            
            this.AddCorsOptions(config);
            this.AddCorsOptions(users);
            this.AddCorsOptions(channels);
            this.AddCorsOptions(messages);
        }

        private void AddCorsOptions(IResource apiResource)
        {
            apiResource.AddMethod("OPTIONS", new MockIntegration(new IntegrationOptions()
            {
                IntegrationResponses = new []
                {
                    new IntegrationResponse()
                    {
                        StatusCode = "200",
                        ResponseParameters = new Dictionary<string, string>()
                        {
                            {"method.response.header.Access-Control-Allow-Headers", "'Content-Type,X-Amz-Date,Authorization,X-Api-Key,X-Amz-Security-Token,X-Amz-User-Agent'"},
                            {"method.response.header.Access-Control-Allow-Origin", "'*'"},
                            {"method.response.header.Access-Control-Allow-Credentials", "'false'"},
                            {"method.response.header.Access-Control-Allow-Methods", "'OPTIONS,GET,PUT,POST,DELETE'"}
                        }
                    }
                },
                PassthroughBehavior = PassthroughBehavior.NEVER,
                RequestTemplates = new Dictionary<string, string>()
                {
                    { "application/json", "{\"statusCode\": 200}"}
                }
            }), new MethodOptions()
            {
                MethodResponses = new []
                {
                    new MethodResponse()
                    {
                        StatusCode = "200",
                        ResponseParameters = new Dictionary<string, bool>()
                        {
                            {"method.response.header.Access-Control-Allow-Headers", true},
                            {"method.response.header.Access-Control-Allow-Methods", true},
                            {"method.response.header.Access-Control-Allow-Credentials", true},
                            {"method.response.header.Access-Control-Allow-Origin", true},
                        }
                    }
                }
            });
        }
    }
}
