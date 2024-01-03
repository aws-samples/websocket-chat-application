# dotnet infrastructure implementation

## Project structure
    
    ├── infrastructure-dotnet               # Infrastructure code via CDK (NET8).
    │   ├── src                             #
    |   |   ├── Infrastruture               # CDK App - Deploys the stacks 
    |   |   |   ├── Dockerfile              # Dockerfile containing the build container definition
    └─────── ...                            # Lambda handler implementations

This project contains the C# version for both infrastructure and lambda handlers. While the CDK project uses dotnetcore3.1, the Lambda handlers are using and running in a NET8 environment. The Lambda build happens in a Docker container, and generates a single executable (bootstrap) that runs in a custom lambda runtime.

The `cdk.json` file tells the CDK Toolkit how to execute your app.

### Deployment

- Bootstrap your AWS account as it's required for the automated Docker image build and deployment
```bash
    cdk bootstrap aws://{ACCOUNT_ID}/{REGION}
```

- Synthesize the cdk stack to emits the synthesized CloudFormation template. Set up will make sure to build and package 
  the lambda functions residing in the [handlers](/infrastructure-net/src/) directory.
```bash
    cdk synth
```

- Deploy the CDK application
```bash
    cdk deploy --all
```

It uses the [.NET Core CLI](https://docs.microsoft.com/dotnet/articles/core/) to compile and execute your project.

## Useful commands

* `dotnet build src` compile this app
* `cdk deploy`       deploy this stack to your default AWS account/region
* `cdk diff`         compare deployed stack with current state
* `cdk synth`        emits the synthesized CloudFormation template