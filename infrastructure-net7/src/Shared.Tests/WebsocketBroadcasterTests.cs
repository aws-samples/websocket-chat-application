using Amazon.DynamoDBv2.DataModel;
using Moq;
using Shared.Models;

namespace Shared.Tests;

public class WebsocketBroadcasterTests
{
    [Test]
    public async Task BroadcastMessage()
    {
        var dbContextMock = new Moq.Mock<IDynamoDBContext>();
        //dbContextMock.Setup(m => m.ScanAsync<Connection>(It.IsAny<IEnumerable<ScanCondition>>()), dbContextMock.Object)
        //    .Returns(() => new List<Connection>());
        var broadcaster = new WebsocketBroadcaster(dbContextMock.Object);
//ScanAsync<Connection>
        await broadcaster.Broadcast("{ \"name\": \"Tamas\" }", "fakeEndpoint");
    }
}