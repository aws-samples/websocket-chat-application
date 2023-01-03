using Amazon.CDK;
using Amazon.CDK.AWS.CloudWatch;
using Constructs;

namespace Infrastructure.Stacks
{
    public class ObservabilityStack : Stack
    {

        internal ObservabilityStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            #region Metrics
            var disconnectionsMetric = new Metric(new MetricProps()
            {
                Namespace = "websocket-chat",
                MetricName = "closedConnection",
                Statistic = "sum"
            });
            
            var newcConnectionsMetric = new Metric(new MetricProps()
            {
                Namespace = "websocket-chat",
                MetricName = "newConnection",
                Statistic = "sum"
            });
            
            var messagesDeliveredMetric = new Metric(new MetricProps()
            {
                Namespace = "websocket-chat",
                MetricName = "messageDelivered",
                Statistic = "sum"
            });
            #endregion
            
            #region Widgets
            var closedConnectionsWidget = new GraphWidget(new GraphWidgetProps()
            {
                Title = "Closed Connections",
                Width = 12,
                Left = new[]{ disconnectionsMetric.With(new MetricOptions()
                {
                    Color = Color.RED
                })}
            });
            
            var newConnectionsWidget = new GraphWidget(new GraphWidgetProps()
            {
                Title = "New Connections",
                Width = 12,
                Left = new[]{ disconnectionsMetric.With(new MetricOptions()
                {
                    Color = Color.GREEN
                })}
            });
            
            var messagesDeliveredWidgets = new GraphWidget(new GraphWidgetProps()
            {
                Title = "Messages Delivered",
                Width = 24,
                Left = new[]{ disconnectionsMetric.With(new MetricOptions()
                {
                    Color = Color.GREEN
                })}
            });
            #endregion

            var dashboard = new Dashboard(this, "Serverless Websocket Chat Dashboard", new DashboardProps()
            {
                Widgets = new []
                {
                    new []
                    {
                        newConnectionsWidget, 
                        closedConnectionsWidget
                    },
                    new []
                    {
                        messagesDeliveredWidgets
                    }
                }
            });
        }
    }
}
