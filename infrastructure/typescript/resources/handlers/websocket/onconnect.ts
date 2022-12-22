// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0

import { APIGatewayProxyEvent, APIGatewayProxyResult } from 'aws-lambda';
import { Tracer } from '@aws-lambda-powertools/tracer';
import { Logger } from '@aws-lambda-powertools/logger';
import { LambdaInterface } from '@aws-lambda-powertools/commons';
import { Metrics, MetricUnits } from '@aws-lambda-powertools/metrics';
import { StatusChangeEvent } from '../../models/status-change-event';
import { Status } from '../../models/status';

const { STATUS_QUEUE_URL, LOG_LEVEL, CONNECTIONS_TABLE_NAME } = process.env;
const logger = new Logger({ serviceName: 'websocketMessagingService', logLevel: LOG_LEVEL });
const tracer = new Tracer({ serviceName: 'websocketMessagingService' });
const metrics = new Metrics({ namespace: 'websocket-chat' });
const AWS = tracer.captureAWS(require('aws-sdk'));
const ddb = tracer.captureAWSClient(new AWS.DynamoDB.DocumentClient({ apiVersion: '2012-08-10', region: process.env.AWS_REGION }));
const SQS = tracer.captureAWSClient(new AWS.SQS());

class Lambda implements LambdaInterface {
    @tracer.captureLambdaHandler()
    public async handler(event: APIGatewayProxyEvent, context: any): Promise<APIGatewayProxyResult> {

        logger.addContext(context);
        logger.debug(JSON.stringify(event));
        logger.debug(JSON.stringify(context));
        let response: APIGatewayProxyResult = { statusCode: 200, body: "OK" };
        let authenticatedCustomerId = event.requestContext.authorizer?.customerId;

        const putParams = {
            TableName: CONNECTIONS_TABLE_NAME,
            Item: {
                connectionId: event.requestContext.connectionId,
                userId: authenticatedCustomerId
            }
        };

        try {
            logger.debug(`Inserting connection details ${JSON.stringify(putParams)}`);
            await ddb.put(putParams).promise();

            metrics.addMetric('newConnection', MetricUnits.Count, 1);
            metrics.publishStoredMetrics();

            // Prepare status change event for broadcast
            let statusChangeEvent = new StatusChangeEvent({
                userId: authenticatedCustomerId,
                currentStatus: Status.ONLINE,
                eventDate: new Date()
            });

            logger.debug("Putting status changed event in the SQS queue:", statusChangeEvent);
            // Put status change event to SQS queue
            let sqsResults = await SQS.sendMessage({
                QueueUrl: STATUS_QUEUE_URL,
                MessageBody: JSON.stringify(statusChangeEvent),
                MessageAttributes: {
                    Type: {
                        StringValue: 'StatusUpdate',
                        DataType: 'String',
                    },
                },
            }).promise();
            logger.debug("queue send result: ", sqsResults);
        } catch (error: any) {
            var body = error.stack || JSON.stringify(error, null, 2);
            response = { statusCode: 500, body: body };
        }

        return response;
    }
}

export const handlerClass = new Lambda();
export const handler = handlerClass.handler;