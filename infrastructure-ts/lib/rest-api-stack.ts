// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0

import { Stack, StackProps } from 'aws-cdk-lib'
import { Runtime } from 'aws-cdk-lib/aws-lambda';
import { Table } from 'aws-cdk-lib/aws-dynamodb';
import { AuthorizationType, CognitoUserPoolsAuthorizer, IResource, LambdaIntegration, MockIntegration, PassthroughBehavior, RestApi } from 'aws-cdk-lib/aws-apigateway';
import { NodejsFunction, NodejsFunctionProps } from 'aws-cdk-lib/aws-lambda-nodejs';
import { Effect, PolicyStatement } from 'aws-cdk-lib/aws-iam';
import { WebSocketApi } from 'aws-cdk-lib/aws-apigatewayv2';
import { Construct } from 'constructs';
import { join } from 'path';
import * as path from 'path';
import { UserPool } from 'aws-cdk-lib/aws-cognito';

export interface RestApiProps extends StackProps {
  messagesTable: Table;
  channelsTable: Table;
  connectionsTable: Table;
  cognitoUserPoolId: string;
  webSocketApi: WebSocketApi;
  logLevel: string;
}

export class RestApiStack extends Stack {

  public apiGatewayEndpoint: string;
  public restApi: RestApi;

  constructor(scope: Construct, id: string, props?: RestApiProps) {
    super(scope, id, props);

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

    const sharedLambdaProps: NodejsFunctionProps = {
      bundling: {
        externalModules: [
        ],
        nodeModules: [
          '@aws-lambda-powertools/logger', 
          '@aws-lambda-powertools/tracer',
          'aws-jwt-verify'
        ],
      },
      depsLockFilePath: join(__dirname, '../resources/', 'package-lock.json'),
      environment: {
        CHANNELS_TABLE_NAME: props?.channelsTable.tableName!,
        CONNECTIONS_TABLE_NAME: props?.connectionsTable.tableName!,
        MESSAGES_TABLE_NAME: props?.messagesTable.tableName!,
        COGNITO_USER_POOL_ID: props?.cognitoUserPoolId!,
        WEBSOCKET_API_URL: `${props?.webSocketApi.apiEndpoint!}/wss`,
        LOG_LEVEL: props?.logLevel!
      },
      runtime: Runtime.NODEJS_20_X,
    }

    // Create a Lambda function for each of the CRUD operations
    const getUsersHandler = new NodejsFunction(this, 'getUsersHandler', {
      entry: path.join(__dirname, `/../resources/handlers/rest/get-users.ts`),
      ...sharedLambdaProps,
    });
    props?.connectionsTable.grantReadData(getUsersHandler);
    getUsersHandler.addToRolePolicy(
      new PolicyStatement({
        effect: Effect.ALLOW,
        actions: ["cognito-idp:ListUsers"],
        resources: [
          `arn:aws:cognito-idp:${Stack.of(this).region}:${Stack.of(this).account}:userpool/${props?.cognitoUserPoolId!}`,
        ],
      })
    );

    const getConfigHandler = new NodejsFunction(this, 'getCConfigHandler', {
      entry: path.join(__dirname, `/../resources/handlers/rest/get-config.ts`),
      ...sharedLambdaProps,
    });
    getConfigHandler.addToRolePolicy(
      new PolicyStatement({
        effect: Effect.ALLOW,
        actions: [
          "ssm:GetParameter",
          "ssm:GetParameters",
          "ssm:GetParametersByPath"
        ],
        resources: [
          `arn:aws:ssm:${Stack.of(this).region}:${Stack.of(this).account}:parameter/prod/cognito/signinurl`,
          `arn:aws:ssm:${Stack.of(this).region}:${Stack.of(this).account}:parameter/prod/websocket/url`,
        ],
      })
    );

    const getChannelsHandler = new NodejsFunction(this, 'getChannelsHandler', {
      entry: path.join(__dirname, `/../resources/handlers/rest/get-channels.ts`),
      ...sharedLambdaProps,
    });

    const postChannelsHandler = new NodejsFunction(this, 'postChannelsHandler', {
      entry: path.join(__dirname, `/../resources/handlers/rest/post-channels.ts`),
      ...sharedLambdaProps,
    });

    const getChannelHandler = new NodejsFunction(this, 'getChannelHandler', {
      entry: path.join(__dirname, `/../resources/handlers/rest/get-channel.ts`),
      ...sharedLambdaProps,
    });

    const getChannelMessagesHandler = new NodejsFunction(this, 'getChannelMessagesHandler', {
      entry: path.join(__dirname, `/../resources/handlers/rest/get-channel-messages.ts`),
      ...sharedLambdaProps,
    });

    // Grant the Lambda functions read/write access to the DynamoDB tables
    props?.channelsTable.grantReadWriteData(getChannelsHandler);
    props?.channelsTable.grantReadData(getChannelsHandler);
    props?.channelsTable.grantReadWriteData(postChannelsHandler);
    props?.channelsTable.grantReadData(getChannelHandler);
    props?.messagesTable.grantReadData(getChannelMessagesHandler);

    // Integrate the Lambda functions with the API Gateway resource
    const getConfigIntegration = new LambdaIntegration(getConfigHandler);
    const getUsersIntegration = new LambdaIntegration(getUsersHandler);
    const getChannelsIntegration = new LambdaIntegration(getChannelsHandler);
    const postChannelsIntegration = new LambdaIntegration(postChannelsHandler);
    const getChannelIntegration = new LambdaIntegration(getChannelHandler);
    const getChannelMessagesIntegration = new LambdaIntegration(getChannelMessagesHandler);

    this.restApi = new RestApi(this, 'ServerlessChatRestApi', {
      restApiName: 'Serverless Chat REST API'
    });

    this.apiGatewayEndpoint = this.restApi.url;

    const userPool = UserPool.fromUserPoolId(this, "UserPool", props?.cognitoUserPoolId!);
    const auth = new CognitoUserPoolsAuthorizer(this, 'websocketChatUsersAuthorizer', {
      cognitoUserPools: [userPool]
    });
    const authMethodOptions = { authorizer: auth, authorizationType: AuthorizationType.COGNITO };

    const api = this.restApi.root.addResource('api');

    const config = api.addResource('config');
    /* [GET]  /config - Retrieve all users with online/offline status */
    config.addMethod('GET', getConfigIntegration);

    const users = api.addResource('users');
    /* [GET]  /users - Retrieve all users with online/offline status */
    users.addMethod('GET', getUsersIntegration, authMethodOptions);

    const channels = api.addResource('channels');
    /* [GET]  /channels - Retrieve all channels with participant details */
    channels.addMethod('GET', getChannelsIntegration, authMethodOptions);
    /* [POST] /channels - Creates a new channel */
    channels.addMethod('POST', postChannelsIntegration, authMethodOptions);
    /* [ANY] /channels/{id} - retrieves channel with specific ID */
    const channelId = channels.addResource('{id}');
    channelId.addMethod('GET', getChannelIntegration, authMethodOptions);

    const messages = channelId.addResource('messages');
    /* [GET]  /channels/{ID}/messages - Retrieve top 100 messages for a specific channel */
    messages.addMethod('GET', getChannelMessagesIntegration, authMethodOptions);

    addCorsOptions(config);
    addCorsOptions(users);
    addCorsOptions(channels);
    addCorsOptions(messages);
  }
};

export function addCorsOptions(apiResource: IResource) {
  apiResource.addMethod('OPTIONS', new MockIntegration({
    integrationResponses: [{
      statusCode: '200',
      responseParameters: {
        'method.response.header.Access-Control-Allow-Headers': "'Content-Type,X-Amz-Date,Authorization,X-Api-Key,X-Amz-Security-Token,X-Amz-User-Agent'",
        'method.response.header.Access-Control-Allow-Origin': "'*'",
        'method.response.header.Access-Control-Allow-Credentials': "'false'",
        'method.response.header.Access-Control-Allow-Methods': "'OPTIONS,GET,PUT,POST,DELETE'",
      },
    }],
    passthroughBehavior: PassthroughBehavior.NEVER,
    requestTemplates: {
      "application/json": "{\"statusCode\": 200}"
    },
  }), {
    methodResponses: [{
      statusCode: '200',
      responseParameters: {
        'method.response.header.Access-Control-Allow-Headers': true,
        'method.response.header.Access-Control-Allow-Methods': true,
        'method.response.header.Access-Control-Allow-Credentials': true,
        'method.response.header.Access-Control-Allow-Origin': true,
      },
    }]
  })
}