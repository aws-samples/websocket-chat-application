using Amazon.CDK;

namespace Infrastructure
{
    public enum LogLevel
    {
        Debug,
        Info,
        Warn,
        Error
    }
    
    sealed class Program
    {
        public static void Main(string[] args)
        {
            var defaultLogLevel = LogLevel.Error;
            var app = new App();
            
            var authStack = new AuthenticationStack(app, "AuthenticationStack", new StackProps{ });
            var databaseStack = new DatabaseStack(app, "DatabaseStack", new StackProps{ });

            var websocketApiStack = new WebsocketApiStack(app, "WebsocketApiStack", new StackProps{ /* TODO: fill in parameters */ });
            var restApiStack = new RestApiStack(app, "RestApiStack", new StackProps{ /* TODO: fill in parameters */ });

            var frontendStack = new FrontendStack(app, "FrontendStack", new FrontendStackProps{ /* TODO: fill in parameters */ });
            var observabilityStack = new ObservabilityStack(app, "ObservabilityStack", new StackProps{ });
            app.Synth();
        }
    }
}
