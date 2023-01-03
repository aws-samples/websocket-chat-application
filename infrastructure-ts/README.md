# Welcome to your Serverless Websocket Chat sample CDK TypeScript project

The `cdk.json` file tells the CDK Toolkit how to execute your app.

## Project structure
    
    ├── infrastructure-ts                   # Infrastructure code via CDK(Typescript).
    │   ├── bin                             # CDK App - Deploys the stacks  
    │   ├── lib                             #
    |   |   ├── auth-stack.ts               # Contains the Cognito Userpool
    |   |   ├── database-stack.ts           # DynamoDB table definitions
    |   |   ├── frontend-stack.ts           # Cloudfront distribution, S3 bucket for static hosting and additional resources
    |   |   ├── rest-api-stack.ts           # ApiGateway REST API to support the frontend application
    |   |   ├── websocket-stack.ts          # ApiGateway Websocket API for real-time communication
    |   |   ├── observability-stack.ts      # CloudWatch Dashboard with custom metrics
    ├── UI                                  # Angular 12 Single Page Application (SPA)
    └── ...


### Deployment

- Restore NPM packages for the project
```bash
    npm install
```

- Bootstrap your AWS account as it's required for the automated Docker image build and deployment
```bash
    cdk bootstrap aws://{ACCOUNT_ID}/{REGION}
```

- Synthesize the cdk stack to emits the synthesized CloudFormation template. Set up will make sure to build and package 
  the lambda functions residing in the [handlers](/infrastructure/resources/handlers) directory.
```bash
    cdk synth
```

- Deploy the CDK application
```bash
    cdk deploy --all
```

## Useful commands

* `npm run build`   compile typescript to js
* `npm run watch`   watch for changes and compile
* `npm run test`    perform the jest unit tests
* `cdk deploy`      deploy this stack to your default AWS account/region
* `cdk diff`        compare deployed stack with current state
* `cdk synth`       emits the synthesized CloudFormation template
