// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0

import { APIGatewayProxyEvent, APIGatewayProxyResult } from 'aws-lambda';
import { Tracer } from '@aws-lambda-powertools/tracer';
import { Logger } from '@aws-lambda-powertools/logger';
import { LambdaInterface } from '@aws-lambda-powertools/commons';
import { Metrics } from '@aws-lambda-powertools/metrics';
import { v4 as uuidv4 } from 'uuid';
import { WebsocketBroadcaster } from '../../utils/websocket-broadcaster';
import { Payload } from '../../models/payload'
import { Message } from '../../models/message';

const { CONNECTIONS_TABLE_NAME, LOG_LEVEL, MESSAGES_TABLE_NAME } = process.env;
const logger = new Logger({ serviceName: 'websocketMessagingService', logLevel: LOG_LEVEL });
const tracer = new Tracer({ serviceName: 'websocketMessagingService' });
const metrics = new Metrics({ namespace: 'websocket-chat'});
const AWS = tracer.captureAWS(require('aws-sdk'));
const ddb = tracer.captureAWSClient(new AWS.DynamoDB.DocumentClient({ apiVersion: '2012-08-10', region: process.env.AWS_REGION }));
const broadcaster = new WebsocketBroadcaster(AWS, metrics, ddb, logger, CONNECTIONS_TABLE_NAME!);

class Lambda implements LambdaInterface {

  private _apiGatewayEndpoint!: string;

  @tracer.captureLambdaHandler()
  public async handler(event: APIGatewayProxyEvent, context: any): Promise<APIGatewayProxyResult> {

    let response: APIGatewayProxyResult = { statusCode: 200, body: "" };
    this._apiGatewayEndpoint = event.requestContext.domainName + '/' + event.requestContext.stage;
    logger.addContext(context);

    try {
      const postObject = JSON.parse(event.body || "").data as Payload;

      // Handle request based on the payload type.
      if (postObject.type == "Message") {
        await this.processMessagePayload(postObject as Message, this._apiGatewayEndpoint);

      } else  {
        logger.info("Unrecognised payload type - ignore processing.");
      }

      metrics.publishStoredMetrics();
    }
    catch (e: any) {
      response = { statusCode: 500, body: e.stack };
    }

    return response;
  }

  async processMessagePayload(payload: Message, apiGatewayEndpoint: string) {
    payload.messageId = uuidv4();
    const messageParams = { TableName: MESSAGES_TABLE_NAME, Item: payload };
    logger.debug(`Inserting message details ${JSON.stringify(messageParams)}`);
    await ddb.put(messageParams).promise();
    logger.debug(`Broadcasting message details ${JSON.stringify(messageParams)}`);
    await broadcaster.broadcast(payload, apiGatewayEndpoint);
  }
}

export const handlerClass = new Lambda();
export const handler = handlerClass.handler;