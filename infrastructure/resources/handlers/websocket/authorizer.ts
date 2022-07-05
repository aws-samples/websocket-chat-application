// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0

import { PolicyDocument } from 'aws-lambda';
import { Tracer } from '@aws-lambda-powertools/tracer';
import { Logger } from '@aws-lambda-powertools/logger';
import { LambdaInterface } from '@aws-lambda-powertools/commons';
import { CognitoJwtVerifier } from "aws-jwt-verify";

const { COGNITO_USER_POOL_ID, LOG_LEVEL } = process.env;
const logger = new Logger({ serviceName: 'websocketMessagingService', logLevel: LOG_LEVEL });
const tracer = new Tracer({ serviceName: 'websocketMessagingService' });
const AWS = tracer.captureAWS(require('aws-sdk'));
const ssm = tracer.captureAWSClient(new AWS.SSM());

class Lambda implements LambdaInterface {
    @tracer.captureLambdaHandler()
    public async handler(event:any, context: any): Promise<any> {

        logger.addContext(context);
        logger.debug(JSON.stringify(event));
        logger.debug(JSON.stringify(context));

        var token = event.headers["Cookie"].split('=')[1];
            
        let cognitoClientIdParameter = await ssm.getParameter({ Name: '/prod/cognito/clientid', WithDecryption: true }).promise();
        logger.debug("Cognito clientId:" + JSON.stringify(cognitoClientIdParameter));
    
        try {
            let cognitoVerifier = CognitoJwtVerifier.create({
              userPoolId: COGNITO_USER_POOL_ID!,
              tokenUse: "id",
              clientId: cognitoClientIdParameter.Parameter.Value
            });
      
            const verifiedToken = await cognitoVerifier.verify(token);
            logger.debug("Token is valid. :", verifiedToken);
            return this.generateAllow(verifiedToken["cognito:username"], event.methodArn);
      
          } catch (err: any) {
            logger.debug("Error during token validation: ", err);
            return this.generateDeny('default', event.methodArn);
        }

        //This code path should never execute - but if it is - Deny access
        return this.generateDeny('default', event.methodArn);
    }

    // Helper function to generate an IAM policy
    generatePolicy(principalId: any, effect: any, resource: any) {
        // Required output:
        var authResponse:any = {
            principalId: principalId
        };
        if (effect && resource) {
            let policyDocument: PolicyDocument = ({
                Version: '2012-10-17', // default version
                Statement: []
            });

            var statementOne = {
                Action: 'execute-api:Invoke', // default action
                Effect: effect,
                Resource: resource,

            };
            policyDocument.Statement[0] = statementOne;
            authResponse.policyDocument = policyDocument;
        }

        // Optional output with custom properties of the String, Number or Boolean type.
        authResponse.context = {
            "customerId": principalId
        };
        return authResponse;
    }

    generateAllow(principalId: any, resource: any) {
        return this.generatePolicy(principalId, 'Allow', resource);
    }

    generateDeny(principalId:any, resource:any) {
        return this.generatePolicy(principalId, 'Deny', resource);
    }
}

export const handlerClass = new Lambda();
export const handler = handlerClass.handler;