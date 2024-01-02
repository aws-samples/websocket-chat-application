using System.Net;
using System.Text;
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
    private readonly IDynamoDBContext _dbContext;
    public WebsocketBroadcaster(IDynamoDBContext dbContext)
    {
        this._dbContext = dbContext;
    }

    [Logging(LogEvent = true, Service = "websocketMessagingService")]
    public async Task Broadcast(string payload, string apiGatewayEndpoint)
    {
        Logger.LogInformation("[Broadcaster] - Retrieving active connections...");
        List<Connection> connectionData;
        try
        {
            connectionData = await _dbContext.ScanAsync<Connection>(Array.Empty<ScanCondition>()).GetRemainingAsync();
            Logger.LogInformation("Retrieved active connections");
            Logger.LogInformation(connectionData);
            
            Logger.LogInformation("Encoding payload to binary:");
            var messageBinary = UTF8Encoding.UTF8.GetBytes(payload);
            Logger.LogInformation(messageBinary);

            // Broadcast message parallel with concurrency limit to avoid API throttling
            var options = new ParallelOptions { MaxDegreeOfParallelism = 5 };
            try
            {
                await Parallel.ForEachAsync(connectionData, options, async (item, token) =>
                {
                    try
                    {
                        Logger.LogInformation($"Broadcasting connection item: {item.connectionId} - {item.userId}");
                        var apiClient = new AmazonApiGatewayManagementApiClient(new AmazonApiGatewayManagementApiConfig
                        {
                            ServiceURL = apiGatewayEndpoint
                        });
                        //Logger.LogInformation(apiClient);
                        var stream = new MemoryStream(messageBinary);
                        var postConnectionRequest = new PostToConnectionRequest
                        {
                            ConnectionId = item.connectionId,
                            Data = stream
                        };

                        Logger.LogInformation($"Broadcast to connection: {item.connectionId}");
                        await apiClient.PostToConnectionAsync(postConnectionRequest);
                        
                        Metrics.AddMetric("messageDelivered", 1, MetricUnit.Count);
                    }
                    catch (AmazonServiceException e)
                    {
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
                    catch (Exception e)
                    {
                        Logger.LogInformation("Error while broadcasting messages!");
                        Logger.LogError(e);
                        Logger.LogError(e.StackTrace);
                    }
                });
            }
            catch (Exception e)
            {
                Logger.LogError($"Error retrieving connections: {e.Message}");
                Logger.LogCritical(e.StackTrace);
            }
        
        }
        catch (Exception e)
        {
            Logger.LogInformation("Error while processing messages in parallel!");
            Logger.LogError(e);
            Logger.LogError(e.StackTrace);
        }
    }
}