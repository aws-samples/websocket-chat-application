using System;
using System.Collections.Generic;
using Amazon.CDK;
using Amazon.CDK.AWS.APIGateway;
using Amazon.CDK.AWS.Apigatewayv2;
using Amazon.CDK.AWS.CloudFront;
using Amazon.CDK.AWS.CloudFront.Origins;
using Amazon.CDK.AWS.Cognito;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.S3.Deployment;
using Amazon.CDK.AWS.SSM;
using Constructs;

namespace Infrastructure.Stacks
{
    public class FrontendStackProps : StackProps
    {
        public RestApi RestApi { get; set; }
        public WebSocketApi WebsocketApi { get; set; }
        public string CognitoUserPoolId { get; set; }
        public string CognitoDomainPrefix { get; set; }
    }
    public class FrontendStack : Stack
    {
        internal FrontendStack(Construct scope, string id, FrontendStackProps props = null) : base(scope, id, props)
        {
            var cloudfrontOAI = new OriginAccessIdentity(this, "cloudfront-OAI", new OriginAccessIdentityProps()
            {
                Comment = $"OAI for {id}"
            });

            var siteBucket = new Bucket(this, "SiteBucket", new BucketProps()
            {
                PublicReadAccess = false,
                Encryption = BucketEncryption.S3_MANAGED,
                BlockPublicAccess = BlockPublicAccess.BLOCK_ALL,
                RemovalPolicy = RemovalPolicy.DESTROY, // NOT recommended for production use
                AutoDeleteObjects = true // NOT recommended for production use
            });
            siteBucket.AddToResourcePolicy(new PolicyStatement(new PolicyStatementProps()
            {
                Effect = Effect.DENY,
                Principals = new []{new AnyPrincipal()},
                Actions = new []{"s3:*"},
                Resources = new[]{siteBucket.BucketArn},
                Conditions = new Dictionary<string, object>()
                {
                    {"Bool", new Dictionary<string,string> { {"aws:SecureTransport", "false" }}}
                }
            }));
            
            // *** Log bucket for cloudfront access logging
            // *** UNCOMMENT TO ENABLE ACCESS LOGGING
            /*var logBucket = new Bucket(this, "LogBucket", new BucketProps()
            {
                PublicReadAccess = false,
                Encryption = BucketEncryption.S3_MANAGED,
                BlockPublicAccess = BlockPublicAccess.BLOCK_ALL,
                RemovalPolicy = RemovalPolicy.DESTROY, // NOT recommended for production use
                AutoDeleteObjects = true // NOT recommended for production use
            });
            logBucket.AddToResourcePolicy(new PolicyStatement(new PolicyStatementProps()
            {
                Effect = Effect.DENY,
                Principals = new []{new AnyPrincipal()},
                Actions = new []{"s3:*"},
                Resources = new[]{logBucket.BucketArn},
                Conditions = new Dictionary<string, object>()
                {
                    {"Bool", new Dictionary<string,string> { {"aws:SecureTransport", "false" }}}
                }
            }));*/
            
            // Grant access to cloudfront
            siteBucket.AddToResourcePolicy(new PolicyStatement(new PolicyStatementProps()
            {
                Actions = new [] { "s3:GetObject" },
                Resources = new[] { siteBucket.ArnForObjects("*") },
                Principals = new IPrincipal[] {new CanonicalUserPrincipal(cloudfrontOAI.CloudFrontOriginAccessIdentityS3CanonicalUserId)}
            }));
            new CfnOutput(this, "Bucket", new CfnOutputProps() { Value = siteBucket.BucketName });

            var distribution = new Distribution(this, "SiteDistribution", new DistributionProps()
            {
                DefaultBehavior = new BehaviorOptions()
                {
                    Origin = new S3Origin(siteBucket, new S3OriginProps(){ OriginAccessIdentity = cloudfrontOAI}),
                    Compress = true,
                    ViewerProtocolPolicy = ViewerProtocolPolicy.REDIRECT_TO_HTTPS,
                    AllowedMethods = AllowedMethods.ALLOW_ALL
                },
                DefaultRootObject = "index.html",
                MinimumProtocolVersion = SecurityPolicyProtocol.TLS_V1_2_2021,
                ErrorResponses = new IErrorResponse[]
                {
                    new ErrorResponse() { ResponsePagePath = "/error.html", HttpStatus = 404, ResponseHttpStatus = 404 },
                    new ErrorResponse() { HttpStatus = 403, ResponseHttpStatus = 200, ResponsePagePath = "/index.html", Ttl = Duration.Minutes(1)}
                },
                // *** UNCOMMENT TO ENABLE ACCESS LOGGING
                // LogBucket = logBucket,
                // LogFilePrefix = "distribution-access-logs/",
                // LogIncludesCookies = true
            });

            var cachePolicy = new CachePolicy(this, "CachePolicy", new CachePolicyProps()
            {
                HeaderBehavior = CacheHeaderBehavior.AllowList("Authorization"),
                CookieBehavior = CacheCookieBehavior.None(),
                QueryStringBehavior = CacheQueryStringBehavior.None(),
                EnableAcceptEncodingBrotli = true,
                EnableAcceptEncodingGzip = true,
                MinTtl = Duration.Seconds(1),
                MaxTtl = Duration.Seconds(10),
                DefaultTtl = Duration.Seconds(5)
            });
            
            // REST API behaviour matched to "api/*" path
            distribution.AddBehavior("api/*", 
                new HttpOrigin($"{props?.RestApi.RestApiId}.execute-api.{Stack.Of(this).Region}.amazonaws.com", new HttpOriginProps()
                {
                    OriginPath = $"/{props?.RestApi.DeploymentStage.StageName}"
                }), 
                new BehaviorOptions() {  
                    AllowedMethods = AllowedMethods.ALLOW_ALL,
                    CachePolicy = cachePolicy,
                    ViewerProtocolPolicy = ViewerProtocolPolicy.HTTPS_ONLY,
                    Compress = false
                }
            );

            var wsOriginRequestPolicy = new OriginRequestPolicy(this, "webSocketPolicy", new OriginRequestPolicyProps()
            {
                OriginRequestPolicyName = "webSocketPolicy",
                Comment = "A default WebSocket policy",
                CookieBehavior = OriginRequestCookieBehavior.All(),
                HeaderBehavior = OriginRequestHeaderBehavior.AllowList(new[]{ "Sec-WebSocket-Key", "Sec-WebSocket-Version", "Sec-WebSocket-Protocol", "Sec-WebSocket-Accept" }),
                QueryStringBehavior = OriginRequestQueryStringBehavior.None()
            });
            
            // Websocket API behaviour matched to "wss/*" path
            distribution.AddBehavior("wss/*", 
                new HttpOrigin($"{props?.WebsocketApi.ApiId}.execute-api.{Stack.Of(this).Region}.amazonaws.com", new HttpOriginProps()
                {
                    OriginPath = $"/"
                }), 
                new BehaviorOptions() {  
                    AllowedMethods = AllowedMethods.ALLOW_GET_HEAD,
                    CachePolicy = CachePolicy.CACHING_DISABLED,
                    OriginRequestPolicy = wsOriginRequestPolicy,
                    ViewerProtocolPolicy = ViewerProtocolPolicy.HTTPS_ONLY
                }
            );
            
            // Upload the pre-compiled frontend static files
            var bucketDeployment = new BucketDeployment(this, $"DeployApp-{DateTime.Now.ToShortDateString()}", new BucketDeploymentProps()
            {
                Sources = new[]{Source.Asset("../UI/dist/websocket-chat")},
                DestinationBucket = siteBucket,
                Distribution = distribution,
                DistributionPaths = new[]{"/"}
            });
            
            // Retrieving Cognito Userpool from existing resource - resolving circular stack dependency
            var userPool = UserPool.FromUserPoolId(this, "UserPool", props.CognitoUserPoolId);
            var appClient = userPool.AddClient("websocket-frontend", new UserPoolClientOptions()
            {
                OAuth = new OAuthSettings()
                {
                    Flows = new OAuthFlows()
                    {
                        AuthorizationCodeGrant = false,
                        ImplicitCodeGrant = true // return ID/Access tokens in returnURL
                    },
                    Scopes = new []{ OAuthScope.OPENID, OAuthScope.PROFILE, OAuthScope.EMAIL },
                    CallbackUrls = new []{ $"https://{distribution.DistributionDomainName}/callback", "http://localhost:4200/callback"},
                    LogoutUrls = new []{ $"https://${distribution.DistributionDomainName}/login", "http://localhost:4200/callback" }
                },
                IdTokenValidity = Duration.Minutes(720)
            });
            
            // Generate a cognito app client with a returnURL pointing to the Cloudfront distribution url
            var domain = userPool.AddDomain("Domain", new UserPoolDomainOptions()
            {
                CognitoDomain = new CognitoDomainOptions()
                {
                    DomainPrefix = props.CognitoDomainPrefix
                }
            });

            var cognitoSignInUrl = domain.SignInUrl(appClient, new SignInUrlOptions()
            {
                RedirectUri = $"https://{distribution.DistributionDomainName}/callback" // must be a URL configured under 'callbackUrls' with the client
            });

            var signinUrlParameter = new StringParameter(this, "CognitoSigninURLParameter", new StringParameterProps()
            {
                AllowedPattern = ".*",
                Description = "Cognito Singin URL",
                ParameterName = "/prod/cognito/signinurl",
                StringValue = cognitoSignInUrl,
                Tier = ParameterTier.STANDARD,
            });
            
            var websocketUrlParameter = new StringParameter(this, "WebsocketURLParameter", new StringParameterProps()
            {
                AllowedPattern = ".*",
                Description = "Websocket API URL",
                ParameterName = "/prod/websocket/url",
                StringValue = $"wss://{distribution.DistributionDomainName}/wss/",
                Tier = ParameterTier.STANDARD,
            });
            
            var clientIdParameter = new StringParameter(this, "CognitoClientIdParameter", new StringParameterProps()
            {
                AllowedPattern = ".*",
                Description = "Cognito client id",
                ParameterName = "/prod/cognito/clientid",
                StringValue = appClient.UserPoolClientId,
                Tier = ParameterTier.STANDARD,
            });
            
            new CfnOutput(this, "cognitoSigninURL", new CfnOutputProps() { Value = cognitoSignInUrl, Description = "SignIn URL for Cognito Userpool"});
            new CfnOutput(this, "DistributionId", new CfnOutputProps() { Value = distribution.DistributionId});
            new CfnOutput(this, "DistributionURL", new CfnOutputProps() { Value = distribution.DistributionDomainName});
        }
    }
}
