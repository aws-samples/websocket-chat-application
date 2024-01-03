using Amazon.CDK;
using Amazon.CDK.AWS.Cognito;
using Amazon.CDK.AWS.Lambda;
using Constructs;

namespace Infrastructure.Stacks
{
    public class AuthenticationStack : Stack
    {
        public UserPool ServerlessUserPool { get; private set; }
        public string CognitoUserPoolId { get; private set; }
        internal AuthenticationStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            var autoVerifyFunction = new Function(this, "autoverify-function", new FunctionProps()
            {
                Runtime = Runtime.NODEJS_16_X,
                MemorySize = 128,
                Timeout = Duration.Seconds(10),
                Handler = "index.handler",
                Code = Code.FromInline(@"exports.handler = (event, context, callback) => {
                    // Autoconfirm user
                    event.response.autoConfirmUser = true;
                    // Return to Amazon Cognito
                    callback(null, event);
                };")
            });

            this.ServerlessUserPool = new UserPool(this, "ServerlessChatUserPool", new UserPoolProps()
            {
                SelfSignUpEnabled = true,
                AutoVerify = new AutoVerifiedAttrs(){ Email = true, Phone = true},
                PasswordPolicy = new PasswordPolicy()
                {
                    MinLength = 12,
                    RequireLowercase = true,
                    RequireUppercase = true,
                    RequireDigits = true,
                    RequireSymbols = true,
                    TempPasswordValidity = Duration.Days(3)
                },
                SignInAliases = new SignInAliases()
                {
                    Username = true,
                    Email = true
                },
                LambdaTriggers = new UserPoolTriggers()
                {
                    PreSignUp = autoVerifyFunction
                }
            });

            this.CognitoUserPoolId = this.ServerlessUserPool.UserPoolId;
        }
    }
}
