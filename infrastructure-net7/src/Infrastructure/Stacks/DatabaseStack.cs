using Amazon.CDK;
using Amazon.CDK.AWS.DynamoDB;
using Constructs;

namespace Infrastructure.Stacks
{
    public class DatabaseStack : Stack
    {
        public Table MessagesTable { get; private set; }
        public Table ChannelsTable { get; private set; }
        public Table ConnectionsTable { get; private set; }

        internal DatabaseStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            this.ConnectionsTable = new Table(this, "Connections", new TableProps()
            {
                PartitionKey = new Attribute() { Name = "connectionId", Type = AttributeType.STRING },
                BillingMode = BillingMode.PAY_PER_REQUEST,
                RemovalPolicy = RemovalPolicy.DESTROY, // NOT recommended for production use
                Encryption = TableEncryption.AWS_MANAGED,
                PointInTimeRecovery = false // set "true" to enable PITR
            });
            
            this.ChannelsTable = new Table(this, "serverless-chat-channels", new TableProps()
            {
                PartitionKey = new Attribute() { Name = "id", Type = AttributeType.STRING },
                TableName = "serverless-chat-channels",
                BillingMode = BillingMode.PAY_PER_REQUEST,
                RemovalPolicy = RemovalPolicy.DESTROY, // NOT recommended for production use
                Encryption = TableEncryption.AWS_MANAGED,
                PointInTimeRecovery = false // set "true" to enable PITR
            });
            
            this.MessagesTable = new Table(this, "serverless-chat-messages", new TableProps()
            {
                PartitionKey = new Attribute() { Name = "channelId", Type = AttributeType.STRING },
                SortKey = new Attribute() { Name = "sentAt", Type = AttributeType.STRING },
                BillingMode = BillingMode.PAY_PER_REQUEST,
                TableName = "serverless-chat-messages",
                RemovalPolicy = RemovalPolicy.DESTROY, // NOT recommended for production use
                Encryption = TableEncryption.AWS_MANAGED,
                PointInTimeRecovery = false // set "true" to enable PITR
            });
        }
    }
}
