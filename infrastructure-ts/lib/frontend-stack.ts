// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0

import { CfnOutput, Duration, RemovalPolicy, Stack, StackProps } from 'aws-cdk-lib'
import { BucketDeployment, Source } from 'aws-cdk-lib/aws-s3-deployment';
import { RestApi } from 'aws-cdk-lib/aws-apigateway';
import { CacheCookieBehavior, CacheHeaderBehavior, CachePolicy, CacheQueryStringBehavior, SecurityPolicyProtocol, ViewerProtocolPolicy } from 'aws-cdk-lib/aws-cloudfront';
import { OAuthScope, UserPool } from 'aws-cdk-lib/aws-cognito';
import { ParameterTier, StringParameter } from 'aws-cdk-lib/aws-ssm';
import { Construct } from 'constructs';
import * as cloudfront from 'aws-cdk-lib/aws-cloudfront';
import * as origins from 'aws-cdk-lib/aws-cloudfront-origins';
import * as iam from 'aws-cdk-lib/aws-iam';
import * as s3 from 'aws-cdk-lib/aws-s3';
import { WebSocketApi } from 'aws-cdk-lib/aws-apigatewayv2';
import { NagSuppressions } from 'cdk-nag';
import { AnyPrincipal, Effect, PolicyStatement } from 'aws-cdk-lib/aws-iam';

export interface FrontendProps extends StackProps {
  restApi: RestApi;
  websocketApi: WebSocketApi;
  cognitoUserPoolId: string;
  cognitoDomainPrefix: string;
}

export class FrontendStack extends Stack {
  constructor(scope: Construct, id: string, props?: FrontendProps) {
    super(scope, id, props);

    const cloudfrontOAI = new cloudfront.OriginAccessIdentity(this, 'cloudfront-OAI', { comment: `OAI for ${id}` });

    // Content bucket
    const siteBucket = new s3.Bucket(this, 'SiteBucket', {
      publicReadAccess: false,
      encryption: s3.BucketEncryption.S3_MANAGED,
      blockPublicAccess: s3.BlockPublicAccess.BLOCK_ALL,
      removalPolicy: RemovalPolicy.DESTROY, // NOT recommended for production use
      autoDeleteObjects: true, // NOT recommended for production use
    });
    siteBucket.addToResourcePolicy(new PolicyStatement({
      effect: Effect.DENY,
      principals: [
        new AnyPrincipal(),
      ],
      actions: [
        "s3:*"
      ],
      resources: [siteBucket.bucketArn],
      conditions: {
        "Bool": { "aws:SecureTransport": "false" },
      },
    }));

    // *** Log bucket for cloudfront access logging
    // *** UNCOMMENT TO ENABLE ACCESS LOGGING
    // const logBucket = new s3.Bucket(this, 'LogBucket', {
    //   publicReadAccess: false,
    //   encryption: s3.BucketEncryption.S3_MANAGED,
    //   blockPublicAccess: s3.BlockPublicAccess.BLOCK_ALL,
    //   removalPolicy: RemovalPolicy.DESTROY, // NOT recommended for production use
    //   autoDeleteObjects: true, // NOT recommended for production use
    // });
    // logBucket.addToResourcePolicy(new PolicyStatement({
    //   effect: Effect.DENY,
    //   principals: [
    //     new AnyPrincipal(),
    //   ],
    //   actions: [
    //     "s3:*"
    //   ],
    //   resources: [logBucket.bucketArn],
    //   conditions: {
    //     "Bool": { "aws:SecureTransport": "false" },
    //   },
    // }));

    // Grant access to cloudfront
    siteBucket.addToResourcePolicy(new PolicyStatement({
      actions: ['s3:GetObject'],
      resources: [siteBucket.arnForObjects('*')],
      principals: [new iam.CanonicalUserPrincipal(cloudfrontOAI.cloudFrontOriginAccessIdentityS3CanonicalUserId)]
    }));
    new CfnOutput(this, 'Bucket', { value: siteBucket.bucketName });

    const distribution = new cloudfront.Distribution(this, 'SiteDistribution', {
      defaultBehavior: { // Default to S3 bucket
        origin: new origins.S3Origin(siteBucket, { originAccessIdentity: cloudfrontOAI }),
        compress: true,
        viewerProtocolPolicy: cloudfront.ViewerProtocolPolicy.REDIRECT_TO_HTTPS,
        allowedMethods: cloudfront.AllowedMethods.ALLOW_ALL,
      },
      defaultRootObject: 'index.html',
      minimumProtocolVersion: SecurityPolicyProtocol.TLS_V1_2_2021,
      errorResponses: [{ responsePagePath: '/error.html', httpStatus: 404, responseHttpStatus: 404 },
      {
        httpStatus: 403,
        responseHttpStatus: 200,
        responsePagePath: '/index.html',
        ttl: Duration.minutes(1),
      }],
      // *** UNCOMMENT TO ENABLE ACCESS LOGGING
      //logBucket: logBucket,
      //logFilePrefix: 'distribution-access-logs/',
      //logIncludesCookies: true
    });
    NagSuppressions.addResourceSuppressions(
      distribution,
      [
        {
          id: 'AwsSolutions-CFR3',
          reason:
            "Access logging is disabled to save cost. It can be re-enabled by uncommenting the code above."
        },
        {
          id: 'AwsSolutions-CFR4',
          reason:
            "TLSv1.1 or TLSv1.2 can be only enforced using a custom certificate with a custom domain alias."
        },
      ],
      true
    );


    // Custom Cloudfront cache policy to forward Authorization header
    const cachePolicy = new CachePolicy(this, 'CachePolicy', {
      headerBehavior: CacheHeaderBehavior.allowList(
        'Authorization',
      ),
      cookieBehavior: CacheCookieBehavior.none(),
      queryStringBehavior: CacheQueryStringBehavior.none(),
      enableAcceptEncodingBrotli: true,
      enableAcceptEncodingGzip: true,
      minTtl: Duration.seconds(1),
      maxTtl: Duration.seconds(10),
      defaultTtl: Duration.seconds(5),
    })

    // REST API behaviour matched to "api/*" path
    distribution.addBehavior('api/*', new origins.HttpOrigin(`${props?.restApi.restApiId}.execute-api.${Stack.of(this).region}.amazonaws.com`, {
      originPath: `/${props?.restApi.deploymentStage.stageName}`
    }), {
      allowedMethods: cloudfront.AllowedMethods.ALLOW_ALL,
      cachePolicy: cachePolicy,
      viewerProtocolPolicy: ViewerProtocolPolicy.HTTPS_ONLY,
      compress: false
    });

    const wsOriginRequestPolicy = new cloudfront.OriginRequestPolicy(this, "webSocketPolicy", {
      originRequestPolicyName: "webSocketPolicy",
      comment: "A default WebSocket policy",
      cookieBehavior: cloudfront.OriginRequestCookieBehavior.all(),
      headerBehavior: cloudfront.OriginRequestHeaderBehavior.allowList("Sec-WebSocket-Key", "Sec-WebSocket-Version", "Sec-WebSocket-Protocol", "Sec-WebSocket-Accept"),
      queryStringBehavior: cloudfront.OriginRequestQueryStringBehavior.none(),
    });

    // Websocket API behaviour matched to "wss/*" path
    distribution.addBehavior('wss/*', new origins.HttpOrigin(`${props?.websocketApi.apiId}.execute-api.${Stack.of(this).region}.amazonaws.com`, {
      originPath: `/`
    }), {
      allowedMethods: cloudfront.AllowedMethods.ALLOW_GET_HEAD,
      cachePolicy: cloudfront.CachePolicy.CACHING_DISABLED,
      originRequestPolicy: wsOriginRequestPolicy,
      viewerProtocolPolicy: ViewerProtocolPolicy.HTTPS_ONLY
    });

    // Upload the pre-compiled frontend static files
    new BucketDeployment(this, `DeployApp-${new Date().toISOString()}`, {
      sources: [Source.asset("../UI/dist/websocket-chat")],
      destinationBucket: siteBucket,
      distribution: distribution,
      distributionPaths: ['/'],
    });

    // Retrieving Cognito Userpool from existing resource - resolving circular stack dependency
    const userPool = UserPool.fromUserPoolId(this, "UserPool", props?.cognitoUserPoolId!);
    const appClient = userPool.addClient('websocket-frontend', {
      oAuth: {
        flows: {
          authorizationCodeGrant: false,
          implicitCodeGrant: true // return ID/Access tokens in returnURL
        },
        scopes: [OAuthScope.OPENID, OAuthScope.PROFILE, OAuthScope.EMAIL],
        callbackUrls: [`https://${distribution.distributionDomainName}/callback`, "http://localhost:4200/callback"],
        logoutUrls: [`https://${distribution.distributionDomainName}/login`, "http://localhost:4200/callback"],
      },

      idTokenValidity: Duration.minutes(720),
    });

    // Generate a cognito app client with a returnURL pointing to the Cloudfront distribution url
    const domain = userPool.addDomain('Domain', {
      cognitoDomain: {
        domainPrefix: props?.cognitoDomainPrefix!
      }
    });

    const cognitoSignInUrl = domain!.signInUrl(appClient!, {
      redirectUri: `https://${distribution.distributionDomainName}/callback`, // must be a URL configured under 'callbackUrls' with the client
    });

    const signinUrlParameter = new StringParameter(this, 'CognitoSigninURLParameter', {
      allowedPattern: '.*',
      description: 'Cognito Singin URL',
      parameterName: '/prod/cognito/signinurl',
      stringValue: cognitoSignInUrl,
      tier: ParameterTier.STANDARD,
    });

    const websocketUrlParameter = new StringParameter(this, 'WebsocketURLParameter', {
      allowedPattern: '.*',
      description: 'Websocket API URL',
      parameterName: '/prod/websocket/url',
      stringValue: `wss://${distribution.distributionDomainName}/wss/`,
      tier: ParameterTier.STANDARD,
    });

    const clientIdParameter = new StringParameter(this, 'CognitoClientIdParameter', {
      allowedPattern: '.*',
      description: 'Cognito client id',
      parameterName: '/prod/cognito/clientid',
      stringValue: appClient?.userPoolClientId!,
      tier: ParameterTier.STANDARD,
    });

    new CfnOutput(this, 'cognitoSigninURL', {
      value: cognitoSignInUrl,
      description: 'SignIn URL for Cognito Userpool',
      exportName: 'cognitoSigninURL',
    });
    new CfnOutput(this, 'DistributionId', { value: distribution.distributionId });
    new CfnOutput(this, 'DistributionURL', { value: distribution.distributionDomainName });
  }
};
