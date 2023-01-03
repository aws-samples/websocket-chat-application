// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0

import { APIGatewayProxyEvent, APIGatewayProxyResult } from 'aws-lambda';
import { Tracer } from '@aws-lambda-powertools/tracer';
import { Logger } from '@aws-lambda-powertools/logger';
import { LambdaInterface } from '@aws-lambda-powertools/commons';

const { LOG_LEVEL } = process.env;
const logger = new Logger({ serviceName: 'websocketMessagingService', logLevel: LOG_LEVEL });
const tracer = new Tracer({ serviceName: 'websocketMessagingService' });
const AWS = tracer.captureAWS(require('aws-sdk'));
const ssm = tracer.captureAWSClient(new AWS.SSM());

class Lambda implements LambdaInterface {
  @tracer.captureLambdaHandler()
  public async handler(event: APIGatewayProxyEvent, context: any): Promise<APIGatewayProxyResult> {

    let response: APIGatewayProxyResult = { statusCode: 200, body: "OK" };
    logger.addContext(context);

    try {

      let cognitoSigninUrlParameter = await ssm.getParameter({Name: '/prod/cognito/signinurl', WithDecryption:true}).promise();
      let websocketUrlParameter = await ssm.getParameter({Name: '/prod/websocket/url', WithDecryption:true}).promise();
      logger.debug("Cognito Signin URL:" + JSON.stringify(cognitoSigninUrlParameter));

      let config =    {
          "api_url": "/api",
          "broadcast_url": websocketUrlParameter.Parameter.Value,
          "login_url": cognitoSigninUrlParameter.Parameter.Value
      }
      response = { statusCode: 200, body: JSON.stringify(config) };

      logger.debug(`Sending config response: ${JSON.stringify(response)}`);
    }
    catch (e: any) {
      logger.debug(JSON.stringify(e));
      response = { statusCode: 500, body: e.stack };
    }

    return response;
  }
}

export const handlerClass = new Lambda();
export const handler = handlerClass.handler;