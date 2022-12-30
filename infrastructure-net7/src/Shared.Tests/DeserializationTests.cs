using System.Text.Json;
using Amazon.Lambda.Serialization.SystemTextJson;
using Shared.Models;

namespace Shared.Tests;

public class DeserializationTests
{
    string payloadJSON = "{\"action\":\"payload\",\"data\":{\"type\":\"Message\",\"sender\":\"tamsant\",\"text\":\"hello\",\"channelId\":\"dgdfgdfg\",\"sentAt\":\"2022-12-30T19:20:45.531Z\"}}";

    [Test]
    public void DeserialiseWebsocketPayload()
    {
        var postObject = JsonSerializer.Deserialize<WebsocketPayload>(payloadJSON);
//System.Text.Json.JsonElement

        var payloadString = ((System.Text.Json.JsonElement)postObject.data).ToString();
        var payload = JsonSerializer.Deserialize<Message>(payloadString);
        if (payload.GetType() == typeof(Message))
        {
            var message = (Message)postObject.data;
            message.messageId = Guid.NewGuid().ToString();
        }
    }
}