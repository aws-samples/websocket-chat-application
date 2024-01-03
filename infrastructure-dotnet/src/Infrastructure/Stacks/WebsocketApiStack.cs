using System.Collections.Generic;
using Amazon.CDK;
using Amazon.CDK.AWS.Apigatewayv2;
using Amazon.CDK.AwsApigatewayv2Authorizers;
using Amazon.CDK.AwsApigatewayv2Integrations;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda.EventSources;
using Amazon.CDK.AWS.SQS;
using Constructs;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Stacks
{
    public class WebsocketApiStackProps : StackProps
    {
        public Table MessagesTable { get; set; }
        public Table ChannelsTable { get; set; }
        public Table ConnectionsTable { get; set; }
        public string CognitoUserPoolId { get; set; }
        public LogLevel LogLevel { get; set; }
    }
    public class WebsocketApiStack : Stack
    {
        public WebSocketApi WebSocketApi { get; set; }
        internal WebsocketApiStack(Construct scope, string id, WebsocketApiStackProps props) : base(scope, id, props)
        {
            // SQS queue for user status updates
            var statusQueue = new Queue(this, "user-status-queue", new QueueProps()
            {
                VisibilityTimeout = Duration.Seconds(30), //default
                ReceiveMessageWaitTime = Duration.Seconds(20), //default
                Encryption = QueueEncryption.KMS_MANAGED
            });
            
            // Enforce TLS calls from any services
            statusQueue.AddToResourcePolicy(new PolicyStatement(new PolicyStatementProps()
            {
                Effect = Effect.DENY,
                Principals = new[]{ new AnyPrincipal()},
                Actions = new[]{ "sqs:*"},
                Resources = new []{statusQueue.QueueArn},
                Conditions = new Dictionary<string, object>()
                {
                    {"Bool", new Dictionary<string,string> { {"aws:SecureTransport", "false" }}}
                }
            }));

            var ssmPolicyStatement = new PolicyStatement(new PolicyStatementProps()
            {
                Effect = Effect.ALLOW,
                Actions = new [] {  
                    "ssm:GetParameter",
                    "ssm:GetParameters",
                    "ssm:GetParametersByPath"
                },
                Resources = new []{ $"arn:aws:ssm:{Stack.Of(this).Region}:{Stack.Of(this).Account}:parameter/prod/cognito/clientid" }
            });

            var defaultLambdaEnvironment = new Dictionary<string, string>()
            {
                {"CONNECTIONS_TABLE_NAME", props?.ConnectionsTable.TableName},
                {"MESSAGES_TABLE_NAME", props?.MessagesTable.TableName},
                {"CHANNELS_TABLE_NAME", props?.ChannelsTable.TableName},
                {"STATUS_QUEUE_URL", statusQueue.QueueUrl},
                {"COGNITO_USER_POOL_ID", props?.CognitoUserPoolId!},
                {"POWERTOOLS_LOG_LEVEL", props?.LogLevel.ToString()}
            };

            #region Lambda handlers
            var authorizerHandler = new CustomRuntimeFunction(this, "AuthorizerHandler",
                "./src/","./WebsocketAuthorizer/src/WebsocketAuthorizer", 
                "bootstrap::WebsocketAuthorizer.Function::FunctionHandler", defaultLambdaEnvironment);
            authorizerHandler.AddToRolePolicy(ssmPolicyStatement);
            
           var onConnectHandler = new CustomRuntimeFunction(this, "OnConnectHandler",
               "./src/","./OnConnect/src/OnConnect", 
                "bootstrap::OnConnect.Function::FunctionHandler", defaultLambdaEnvironment);
            props?.ConnectionsTable.GrantReadWriteData(onConnectHandler);
            statusQueue.GrantSendMessages(onConnectHandler);

            var onDisconnectHandler = new CustomRuntimeFunction(this, "OnDisconnectHandler",
                "./src/","./OnDisconnect/src/OnDisconnect", 
                "bootstrap::OnDisconnect.Function::FunctionHandler", defaultLambdaEnvironment);
            props?.ConnectionsTable.GrantReadWriteData(onDisconnectHandler);
            statusQueue.GrantSendMessages(onDisconnectHandler);
            
            var onMessageHandler = new CustomRuntimeFunction(this, "OnMessageHandler",
                "./src/","./OnMessage/src/OnMessage", 
                "bootstrap::OnMessage.Function::FunctionHandler", defaultLambdaEnvironment);
            onMessageHandler.AddToRolePolicy(ssmPolicyStatement);
            props?.ConnectionsTable.GrantReadWriteData(onMessageHandler);
            props?.MessagesTable.GrantReadWriteData(onMessageHandler);
            #endregion

            var authorizer = new WebSocketLambdaAuthorizer("Authorizer", authorizerHandler,
                new WebSocketLambdaAuthorizerProps()
                {
                    IdentitySource = new []{ "route.request.header.Cookie" }
                });

            this.WebSocketApi = new WebSocketApi(this, "ServerlessChatWebsocketApi", new WebSocketApiProps()
            {
                ApiName = "Serverless Chat Websocket API",
                ConnectRouteOptions = new WebSocketRouteOptions()
                {
                    Authorizer = authorizer,
                    Integration = new WebSocketLambdaIntegration("ConnectIntegration", onConnectHandler)
                },
                DisconnectRouteOptions = new WebSocketRouteOptions()
                {
                    Integration = new WebSocketLambdaIntegration("DisconnectIntegration", onDisconnectHandler)
                },
                DefaultRouteOptions = new WebSocketRouteOptions()
                {
                    Integration = new WebSocketLambdaIntegration("DefaultIntegration", onMessageHandler)
                }
            });

            var prodStage = new WebSocketStage(this, "Prod", new WebSocketStageProps()
            {
                WebSocketApi = this.WebSocketApi,
                StageName = "wss",
                AutoDeploy = true
            });
            
            defaultLambdaEnvironment.Add("APIGW_ENDPOINT", prodStage.Url.Replace("wss://", ""));
            var userStatusBroadcastHandler = new CustomRuntimeFunction(this, "UserStatusBroadcastHandler",
                "./src/","./StatusBroadcast/src/StatusBroadcast", 
                "bootstrap::StatusBroadcast.Function::FunctionHandler", defaultLambdaEnvironment);
            userStatusBroadcastHandler.AddEventSource(new SqsEventSource(statusQueue, new SqsEventSourceProps()
            {
                BatchSize = 10, //default
                MaxBatchingWindow = Duration.Minutes(0),
                ReportBatchItemFailures = true //default to false
            }));
            statusQueue.GrantConsumeMessages(userStatusBroadcastHandler);
            props?.ConnectionsTable.GrantReadWriteData(userStatusBroadcastHandler);

            this.WebSocketApi.GrantManageConnections(onMessageHandler);
            this.WebSocketApi.GrantManageConnections(userStatusBroadcastHandler);
        }
    }
}
