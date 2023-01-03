namespace Shared;

public static class Constants
{
    public static class EnvironmentVariables
    {
        public static string StatusQueueUrl = "STATUS_QUEUE_URL";
        public static string LogLevel = "LOG_LEVEL";
        public static string ConnectionsTableName = "CONNECTIONS_TABLE_NAME";
        public static string ChannelsTableName = "CHANNELS_TABLE_NAME";
        public static string MessagesTableName = "MESSAGES_TABLE_NAME";
        public static string ApiGatewayEndpoint = "APIGW_ENDPOINT";
        public static string CognitoUserPoolId = "COGNITO_USER_POOL_ID";
        public static string AwsRegion = "AWS_REGION";
    }

    public static class SSMParameters
    {
        public static string CognitoClientId = "/prod/cognito/clientid";
        public static string CognitoSigninUrl = "/prod/cognito/signinurl";
        public static string WebsocketApiUrl = "/prod/websocket/url";
    }
}