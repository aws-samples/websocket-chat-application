using System.Text.Json.Serialization;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.SQSEvents;
using Shared.Models;

namespace Shared;

[JsonSerializable(typeof(APIGatewayProxyRequest))]
[JsonSerializable(typeof(APIGatewayProxyResponse))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(Connection))]
[JsonSerializable(typeof(Config))]
[JsonSerializable(typeof(Payload))]
[JsonSerializable(typeof(Message))]
[JsonSerializable(typeof(StatusChangeEvent))]
[JsonSerializable(typeof(Channel))]
[JsonSerializable(typeof(Status))]
[JsonSerializable(typeof(SQSEvent))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(APIGatewayCustomAuthorizerRequest))]
public partial class CustomJsonSerializerContext : JsonSerializerContext
{
}
