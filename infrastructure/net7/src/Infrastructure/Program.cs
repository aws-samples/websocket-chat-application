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
            
            app.Synth();
        }
    }
}
