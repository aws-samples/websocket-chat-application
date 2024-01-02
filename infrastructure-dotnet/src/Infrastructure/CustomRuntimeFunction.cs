using System.Collections.Generic;
using Amazon.CDK;
using Amazon.CDK.AWS.Lambda;
using Amazon.JSII.Runtime.Deputy;
using Constructs;

namespace Infrastructure
{
    /// <summary>
    /// This class encapsulates the building and configuration of a dotnet Lambda function using a custom runtime.
    /// </summary>
    public class CustomRuntimeFunction : Function
    {
        public CustomRuntimeFunction(Construct scope, string id, string mountPoint, string assetSourcePath,
            string handler, IDictionary<string, string> env) : base(scope, id,
            CreateFunctionProps(mountPoint, assetSourcePath, handler, env))
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

        /// <summary>
        /// Builds the Lambda function properties
        /// </summary>
        /// <param name="mountPath">Root directory to mount under /asset-input in the docker container</param>
        /// <param name="assetSourcePath">Relative path to project directory. No trailing slash.</param>
        /// <param name="handler">Path to handler function in the standard Lambda function name format.</param>
        /// <param name="env">Environment variables to inject to the Lambda function</param>
        /// <returns>Fully populated FunctionProps object.</returns>
        static FunctionProps CreateFunctionProps(string mountPath, string assetSourcePath, string handler,
            IDictionary<string, string> env)
        {
            var assetSourcePathTrimmed = assetSourcePath.Substring(2, assetSourcePath.Length - 2);
            
            string[] defaultLambdaPackagingCommands = new string[]
            {
                // enter project directory
                $"cd {assetSourcePath}",
                // dotnet requires write permissions during build - let's use /tmp
                "export HOME=\"/tmp\"",
                "export DOTNET_CLI_HOME=\"/tmp/DOTNET_CLI_HOME\"",
                "export PATH=\"$PATH:/tmp/DOTNET_CLI_HOME/.dotnet/tools\"",
                $"NUGET_PACKAGES=\"/.nuget-cache\"",
                // restore project dependencies
                $"dotnet restore --packages /.nuget-cache --use-current-runtime", 
                // publish a standalone bootstrap executable - Trimming ENABLED
                //"dotnet publish -c Release --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=True -p:TrimMode=link",
                // publish a standalone bootstrap executable - Trimming DISABLED
                "dotnet publish -c Release --no-restore --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true",
                // copy bootstrap library to /asset-output for CDK 
                $"cp -r /asset-input/{assetSourcePathTrimmed}/bin/Release/net8.0/linux-x64/publish/bootstrap /asset-output"
            };

            var dockerVolume = new DockerVolume {
                ContainerPath = "/.nuget-cache",
                HostPath = $"{mountPath}.nuget-cache",

                // the properties below are optional
                Consistency = DockerVolumeConsistency.CONSISTENT,
            };

            return new FunctionProps
            {
                Runtime = Runtime.PROVIDED_AL2,
                Code = Code.FromAsset(mountPath, new Amazon.CDK.AWS.S3.Assets.AssetOptions
                {   
                    Exclude = new []{
                        "**/cdk.out",
                        "**/.dockerignore",
                        "**/.env",
                        "**/.git",
                        "**/.gitignore",
                        "**/.project",
                        "**/.settings",
                        "**/.toolstarget",
                        "**/.vs",
                        "**/.vscode",
                        "**/.idea",
                        "**/*.*proj.user",
                        "**/*.dbmdl",
                        "**/*.jfm",
                        "**/azds.yaml",
                        "**/bin",
                        "**/charts",
                        "**/docker-compose*",
                        "**/Dockerfile*",
                        "**/node_modules",
                        "**/npm-debug.log",
                        "**/obj",
                        "**/secrets.dev.yaml",
                        "**/values.dev.yaml",
                        "LICENSE",
                        "README.md",},
                    Bundling = new BundlingOptions
                    {
                        Image = DockerImage.FromBuild("./src/"), // Dockerfile in {repo-root}/infrastructure-dotnet/src/
                        Command = new[]
                        {
                            "bash", "-c", string.Join(" && ", defaultLambdaPackagingCommands)
                        },
                        Volumes = new[] {dockerVolume}
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