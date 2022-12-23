using System.Collections.Generic;
using Amazon.CDK;
using Amazon.CDK.AWS.Lambda;
using Amazon.JSII.Runtime.Deputy;
using Constructs;

namespace Infrastructure
{
    public class CustomRuntimeFunction : Function
    {
        private static readonly string[] defaultLambdaPackagingCommands = new string[]
        {
            "export HOME=\"/tmp\"",
            "export DOTNET_CLI_HOME=\"/tmp/DOTNET_CLI_HOME\"",
            "export PATH=\"$PATH:/tmp/DOTNET_CLI_HOME/.dotnet/tools\"",
            "dotnet restore",
            "dotnet publish -c Release --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=True -p:TrimMode=link",
            "cp -r /asset-input/bin/Release/net7.0/linux-x64/publish/bootstrap /asset-output"
        };

        public CustomRuntimeFunction(Construct scope, string id, string assetSourcePath, string handler, IDictionary<string, string> env) : base(scope, id, CreateFunctionProps(assetSourcePath, handler, env))
        {
        }

        #region Base Overrides
        protected CustomRuntimeFunction(ByRefValue reference) : base(reference)
        {
        }

        protected CustomRuntimeFunction(DeputyProps props) : base(props)
        {
        }
        
        public CustomRuntimeFunction(Construct scope, string id, IFunctionProps props) : base(scope, id, props)
        {
        }
        #endregion

        static FunctionProps CreateFunctionProps(string assetSourcePath, string handler, IDictionary<string, string> env)
        {
            return new FunctionProps
            {
                Runtime = Runtime.PROVIDED_AL2,
                Code = Code.FromAsset(assetSourcePath, new Amazon.CDK.AWS.S3.Assets.AssetOptions
                {
                    Bundling = new BundlingOptions
                    {
                        Image = DockerImage.FromBuild("./"),
                        Command = new[]
                        {
                            "bash", "-c", string.Join(" && ", defaultLambdaPackagingCommands)
                        }
                    }
                }),
                Environment = env,
                Handler = handler,
                Tracing = Tracing.ACTIVE,
                Architecture = Architecture.X86_64,
                MemorySize = 1024
            };
        }
    }
}