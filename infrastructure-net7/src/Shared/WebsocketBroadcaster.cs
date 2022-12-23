using System.Net;
using System.Text;
using System.Text.Json;
using Amazon.ApiGatewayManagementApi;
using Amazon.ApiGatewayManagementApi.Model;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Runtime;
using AWS.Lambda.Powertools.Logging;
using AWS.Lambda.Powertools.Metrics;
using AWS.Lambda.Powertools.Tracing;
using Shared.Models;

namespace Shared;

public class WebsocketBroadcaster
{
    private readonly DynamoDBContext _dbContext;
    public WebsocketBroadcaster(DynamoDBContext dbContext)
    {
        this._dbContext = dbContext;
    }
    
    [Logging(LogEvent = true, Service = "websocketMessagingService")]
    [Metrics(Namespace = "websocket-chat")]
    [Tracing(CaptureMode = TracingCaptureMode.ResponseAndError, Namespace = "websocket-chat")]
    public async Task Broadcast(Message message, string apiGatewayEndpoint)
    {
        Logger.LogInformation("[Broadcaster] - Retrieving active connections...");
        var connectionData = await _dbContext.ScanAsync<Connection>(Array.Empty<ScanCondition>()).GetRemainingAsync();
        var apiClient = new AmazonApiGatewayManagementApiClient(new AmazonApiGatewayManagementApiConfig {
            ServiceURL = apiGatewayEndpoint
        });

        var stream = new MemoryStream(UTF8Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message)));
        foreach(var item in connectionData) {
            var postConnectionRequest = new PostToConnectionRequest {
                ConnectionId = item.connectionId,
                Data = stream
            };
            try {
                Logger.LogInformation($"Broadcast to connection: {item.connectionId}");
                stream.Position = 0;
                await apiClient.PostToConnectionAsync(postConnectionRequest);
            } catch (AmazonServiceException e) {
                // API Gateway returns a status of 410 GONE when the connection is no
                // longer available. If this happens, we simply delete the identifier
                // from our DynamoDB table.
                if (e.StatusCode == HttpStatusCode.Gone)
                {
                    Logger.LogInformation($"Deleting stale connection: {item.connectionId}");
                    await _dbContext.DeleteAsync(item);
                } 
                else 
                {
                    Logger.LogError($"Error posting message to {item.connectionId}: {e.Message}");
                    Logger.LogCritical(e.StackTrace);
                }
            }
        }
    }
}