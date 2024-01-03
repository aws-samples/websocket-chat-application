// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0

import { Duration, Stack, StackProps } from 'aws-cdk-lib'
import { NodejsFunction, NodejsFunctionProps } from 'aws-cdk-lib/aws-lambda-nodejs';
import { WebSocketApi, WebSocketStage } from 'aws-cdk-lib/aws-apigatewayv2';
import { WebSocketLambdaAuthorizer } from 'aws-cdk-lib/aws-apigatewayv2-authorizers';
import { WebSocketLambdaIntegration } from 'aws-cdk-lib/aws-apigatewayv2-integrations';
import { Runtime, Tracing } from 'aws-cdk-lib/aws-lambda';
import { Table } from 'aws-cdk-lib/aws-dynamodb';
import { SqsEventSource } from 'aws-cdk-lib/aws-lambda-event-sources';
import { AnyPrincipal, Effect, PolicyStatement } from 'aws-cdk-lib/aws-iam';
import { Construct } from 'constructs';
import { join } from 'path';
import { NagSuppressions } from 'cdk-nag';
import * as path from 'path';
import * as sqs from 'aws-cdk-lib/aws-sqs';

export interface WebsocketProps extends StackProps {
  messagesTable: Table;
  channelsTable: Table;
  connectionsTable: Table;
  cognitoUserPoolId: string;
  logLevel: string;
}

export class WebsocketStack extends Stack {

  public webSocketApi: WebSocketApi;

  constructor(scope: Construct, id: string, props?: WebsocketProps) {
    super(scope, id, props);

    // SQS queue for user status updates
    const statusQueue = new sqs.Queue(this, 'user-status-queue', {
      visibilityTimeout: Duration.seconds(30),      // default,
      receiveMessageWaitTime: Duration.seconds(20), // default
      encryption: sqs.QueueEncryption.KMS_MANAGED
    });
    // Enforce TLS calls from any services
    statusQueue.addToResourcePolicy(new PolicyStatement({
      effect: Effect.DENY,
      principals: [
          new AnyPrincipal(),
      ],
      actions: [
          "sqs:*"
      ],
      resources: [statusQueue.queueArn],
      conditions: {
          "Bool": {"aws:SecureTransport": "false"},
      },
    }));
    NagSuppressions.addResourceSuppressions(
      statusQueue,
      [
        {
          id: 'AwsSolutions-SQS3',
          reason:
            "Supress warning about missing DLQ. DLQ is not mission-critical here, a missing status update won't cause service disruptuion.",
        },
      ],
      true
    );

    var ssmPolicyStatement = new PolicyStatement({
      effect: Effect.ALLOW,
      actions: [
        "ssm:GetParameter",
        "ssm:GetParameters",
        "ssm:GetParametersByPath"
      ],
      resources: [
        `arn:aws:ssm:${Stack.of(this).region}:${Stack.of(this).account}:parameter/prod/cognito/clientid`,
      ],
    })

    const nodeJsFunctionProps: NodejsFunctionProps = {
      bundling: {
        externalModules: [
        ],
        nodeModules: [
          '@aws-lambda-powertools/logger', 
          '@aws-lambda-powertools/tracer',
          'aws-jwt-verify',
          '@aws-lambda-powertools/metrics'
        ],
      },
      depsLockFilePath: join(__dirname, '../resources/', 'package-lock.json'),
      environment: {
        CONNECTIONS_TABLE_NAME: props?.connectionsTable.tableName!,
        MESSAGES_TABLE_NAME: props?.messagesTable.tableName!,
        CHANNELS_TABLE_NAME: props?.channelsTable.tableName!,
        STATUS_QUEUE_URL: statusQueue.queueUrl,
        COGNITO_USER_POOL_ID: props?.cognitoUserPoolId!,
        LOG_LEVEL: props?.logLevel!
      },
      handler: "handler",
      runtime: Runtime.NODEJS_20_X,
      tracing: Tracing.ACTIVE
    }

    const authorizerHandler = new NodejsFunction(this, "AuthorizerHandler", {
      entry: path.join(__dirname, `/../resources/handlers/websocket/authorizer.ts`),
      ...nodeJsFunctionProps
    });
    authorizerHandler.addToRolePolicy(ssmPolicyStatement);

    const onConnectHandler = new NodejsFunction(this, "OnConnectHandler", {
      entry: path.join(__dirname, `/../resources/handlers/websocket/onconnect.ts`),
      ...nodeJsFunctionProps
    });
    props?.connectionsTable.grantReadWriteData(onConnectHandler);
    statusQueue.grantSendMessages(onConnectHandler);

    const onDisconnectHandler = new NodejsFunction(this, "OnDisconnectHandler", {
      entry: path.join(__dirname, `/../resources/handlers/websocket/ondisconnect.ts`),
      ...nodeJsFunctionProps
    });
    props?.connectionsTable.grantReadWriteData(onDisconnectHandler);
    statusQueue.grantSendMessages(onDisconnectHandler);

    const onMessageHandler = new NodejsFunction(this, "OnMessageHandler", {
      entry: path.join(__dirname, `/../resources/handlers/websocket/onmessage.ts`),
      ...nodeJsFunctionProps
    });
    onMessageHandler.addToRolePolicy(ssmPolicyStatement);
    props?.connectionsTable.grantReadWriteData(onMessageHandler);
    props?.messagesTable.grantReadWriteData(onMessageHandler);


    const authorizer = new WebSocketLambdaAuthorizer('Authorizer', authorizerHandler, { identitySource: ['route.request.header.Cookie'] });
    this.webSocketApi = new WebSocketApi(this, 'ServerlessChatWebsocketApi', {
      apiName: 'Serverless Chat Websocket API',
      connectRouteOptions: { integration: new WebSocketLambdaIntegration("ConnectIntegration", onConnectHandler), authorizer },
      disconnectRouteOptions: { integration: new WebSocketLambdaIntegration("DisconnectIntegration", onDisconnectHandler) },
      defaultRouteOptions: { integration: new WebSocketLambdaIntegration("DefaultIntegration", onMessageHandler) },
    });

    const prodStage = new WebSocketStage(this, 'Prod', {
      webSocketApi: this.webSocketApi,
      stageName: 'wss',
      autoDeploy: true,
    });

    nodeJsFunctionProps.environment!["APIGW_ENDPOINT"] = prodStage.url.replace('wss://', '');

    const userStatusBroadcastHandler = new NodejsFunction(this, "userStatusBroadcastHandler", {
      entry: path.join(__dirname, `/../resources/handlers/websocket/status-broadcast.ts`),
      ...nodeJsFunctionProps
    });
    userStatusBroadcastHandler.addEventSource(new SqsEventSource(statusQueue, {
      batchSize: 10, // default
      maxBatchingWindow: Duration.minutes(0),
      reportBatchItemFailures: true, // default to false
    }));
    statusQueue.grantConsumeMessages(userStatusBroadcastHandler);
    props?.connectionsTable.grantReadWriteData(userStatusBroadcastHandler);

    this.webSocketApi.grantManageConnections(onMessageHandler);
    this.webSocketApi.grantManageConnections(userStatusBroadcastHandler);
  }
}
