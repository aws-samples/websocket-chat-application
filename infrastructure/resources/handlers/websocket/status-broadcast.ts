// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0

import { APIGatewayProxyResult } from 'aws-lambda';
import { Tracer } from '@aws-lambda-powertools/tracer';
import { Logger } from '@aws-lambda-powertools/logger';
import { LambdaInterface } from '@aws-lambda-powertools/commons';
import { Metrics } from '@aws-lambda-powertools/metrics';
import { SQSEvent } from 'aws-lambda/trigger/sqs';
import { StatusChangeEvent } from '../../models/status-change-event';
import { WebsocketBroadcaster } from '../../utils/websocket-broadcaster';

const { CONNECTIONS_TABLE_NAME, LOG_LEVEL, APIGW_ENDPOINT } = process.env;
const logger = new Logger({ serviceName: 'websocketMessagingService', logLevel: LOG_LEVEL });
const tracer = new Tracer({ serviceName: 'websocketMessagingService' });
const metrics = new Metrics({ namespace: 'websocket-chat', serviceName: 'websocketMessagingService' });
const AWS = tracer.captureAWS(require('aws-sdk'));
const ddb = tracer.captureAWSClient(new AWS.DynamoDB.DocumentClient({ apiVersion: '2012-08-10', region: process.env.AWS_REGION }));
const broadcaster = new WebsocketBroadcaster(AWS, metrics, ddb, logger, CONNECTIONS_TABLE_NAME!);

class Lambda implements LambdaInterface {
  @tracer.captureLambdaHandler()
  public async handler(event: SQSEvent, context: any): Promise<any> {

    let response: APIGatewayProxyResult = { statusCode: 200, body: "" };
    logger.addContext(context);

    try {
      logger.debug(`Triggered SQS processor lambda with payload: ${JSON.stringify(event)}`);
      logger.debug(`ApiGatewayUrl: ${APIGW_ENDPOINT}`);

      await Promise.all(event.Records.map(async (record: any) => {
        let statuschangeEvent = JSON.parse(record.body) as StatusChangeEvent;
        await broadcaster.broadcast(statuschangeEvent, APIGW_ENDPOINT!);

        logger.debug(`Event record has been processed: ${record.body}`);
      }));
    }
    catch (e: any) {
      response = { statusCode: 500, body: e.stack };
    }

    return response;
  }
}

export const handlerClass = new Lambda();
export const handler = handlerClass.handler;