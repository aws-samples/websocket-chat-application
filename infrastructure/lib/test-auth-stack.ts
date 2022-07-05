// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0

import { Duration, Stack, StackProps } from 'aws-cdk-lib'
import * as cognito from 'aws-cdk-lib/aws-cognito';
import { OAuthScope } from 'aws-cdk-lib/aws-cognito';
import { Construct } from 'constructs';
import { AssetCode, Code, Function, Runtime } from 'aws-cdk-lib/aws-lambda';

// Test auth configuration to use with a locally hosted frontend
export class TestAuthenticationStack extends Stack {

  constructor(scope: Construct, id: string, props?: StackProps) {
    super(scope, id, props);

    const autoVerifyFunction = new Function(this, 'lambda-function', {
      runtime: Runtime.NODEJS_14_X,
      memorySize: 128,
      timeout: Duration.seconds(5),
      handler: 'index.handler',
      code: Code.fromInline(`exports.handler = (event, context, callback) => {
        // Autoconfirm user
        event.response.autoConfirmUser = true;
        // Return to Amazon Cognito
        callback(null, event);
        };`)
    });

    const userPool = new cognito.UserPool(this, 'TestUserPool', {
      selfSignUpEnabled: true,
      autoVerify: { email: true, phone: true },
      signInAliases: {
        username: true,
        email: true,
      },
      lambdaTriggers: {
        preSignUp: autoVerifyFunction
      }
    });

    const appClient = userPool.addClient('websocket-frontend', {
      oAuth: {
        flows: {
          authorizationCodeGrant: false,
          implicitCodeGrant: true // return ID/Access tokens in returnURL
        },
        scopes: [OAuthScope.OPENID, OAuthScope.PROFILE, OAuthScope.EMAIL],
        callbackUrls: [`https://localhost:4200/callback`],
        logoutUrls: [`https://localhost:4200/login`],
      },

      idTokenValidity: Duration.minutes(720),
    });

    //
    //  Generate a cognito app client with a returnURL pointing to the Cloudfront distribution url
    //
    const domain = userPool.addDomain('Domain', {
      cognitoDomain: {
        domainPrefix: "test-userpool-users" // TODO: extract to stack parameter
      }
    });

    const cognitoSignInUrl = domain!.signInUrl(appClient!, {
      redirectUri: `https://localhost:4200/callback`, // must be a URL configured under 'callbackUrls' with the client
    });


  }
};