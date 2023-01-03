// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0

import { APIGatewayProxyEvent, APIGatewayProxyResult } from 'aws-lambda';
import { Tracer } from '@aws-lambda-powertools/tracer';
import { Logger } from '@aws-lambda-powertools/logger';
import { LambdaInterface } from '@aws-lambda-powertools/commons';
import { User } from '../../models/user';
import { Status } from '../../models/status';

const { CONNECTIONS_TABLE_NAME, LOG_LEVEL, COGNITO_USER_POOL_ID } = process.env;
const logger = new Logger({ serviceName: 'websocketMessagingService', logLevel: LOG_LEVEL });
const tracer = new Tracer({ serviceName: 'websocketMessagingService' });
const AWS = tracer.captureAWS(require('aws-sdk'));
const ddb = tracer.captureAWSClient(new AWS.DynamoDB.DocumentClient({ apiVersion: '2012-08-10', region: process.env.AWS_REGION }));
const cognito = tracer.captureAWSClient(new AWS.CognitoIdentityServiceProvider());

class Lambda implements LambdaInterface {
  @tracer.captureLambdaHandler()
  public async handler(event: APIGatewayProxyEvent, context: any): Promise<APIGatewayProxyResult> {

    let response: APIGatewayProxyResult = { statusCode: 200, body: "OK" };
    logger.addContext(context);

    try {

      // Get online users from connection table
      logger.debug('Retrieving active connections...');
      let connectionData = await ddb.scan({ TableName: CONNECTIONS_TABLE_NAME }).promise();
      logger.debug("DDB users: " + JSON.stringify(connectionData));

      // Get all cognito users
      var params = {
        UserPoolId: COGNITO_USER_POOL_ID
      };
      let cognitoUsers = await cognito.listUsers(params).promise();
      logger.debug("Cognito users: " +  JSON.stringify(cognitoUsers));

      // Merge list into response format
      let userList: User[] = cognitoUsers.Users.map((user:any)=> {
        let userIsConnected = connectionData.Items.find(((u: { userId: any; }) => u.userId === user.Username));
        return new User({
            username: user.Username,
            status: userIsConnected ? Status.ONLINE : Status.OFFLINE
        });
      });
      logger.debug('Compiled user list: ' + JSON.stringify(userList));

      // Send response
      response = { statusCode: 200, body: JSON.stringify(userList) };
    }
    catch (e: any) {
      response = { statusCode: 500, body: e.stack };
    }

    return response;
  }
}

export const handlerClass = new Lambda();
export const handler = handlerClass.handler;