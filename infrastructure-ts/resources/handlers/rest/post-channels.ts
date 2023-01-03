// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0

import { APIGatewayProxyEvent, APIGatewayProxyResult } from 'aws-lambda';
import { Tracer } from '@aws-lambda-powertools/tracer';
import { Logger } from '@aws-lambda-powertools/logger';
import { LambdaInterface } from '@aws-lambda-powertools/commons';
import { Metrics } from '@aws-lambda-powertools/metrics';
import { Channel } from '../../models/channel'

const { CHANNELS_TABLE_NAME, LOG_LEVEL } = process.env;
const logger = new Logger({ serviceName: 'websocketMessagingService', logLevel: LOG_LEVEL });
const tracer = new Tracer({ serviceName: 'websocketMessagingService' });
const AWS = tracer.captureAWS(require('aws-sdk'));
const ddb = tracer.captureAWSClient(new AWS.DynamoDB.DocumentClient({ apiVersion: '2012-08-10', region: process.env.AWS_REGION }));

class Lambda implements LambdaInterface {
  @tracer.captureLambdaHandler()
  public async handler(event: APIGatewayProxyEvent, context: any): Promise<APIGatewayProxyResult> {

    let response: APIGatewayProxyResult = { statusCode: 200, body: "" };

    logger.addContext(context);

    try {
      const postData: Channel = JSON.parse(event.body!);
      logger.debug(`POST data: ${JSON.stringify(postData)}`);

      const channelParams = {
        TableName: CHANNELS_TABLE_NAME,
        Item: {
          id: postData.id
        }
      };

      logger.debug(`Inserting channel details ${JSON.stringify(channelParams)}`);
      await ddb.put(channelParams).promise();

      logger.debug(JSON.stringify(event));
      logger.debug('Post Channel executed successfully!');
    }
    catch (e: any) {
      response = { statusCode: 500, body: e.stack };
    }

    return response;
  }
}

export const handlerClass = new Lambda();
export const handler = handlerClass.handler;