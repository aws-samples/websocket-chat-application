using System.Text.Json.Serialization;
using Amazon.Lambda.APIGatewayEvents;
using Shared.Models;

namespace Shared;

[JsonSerializable(typeof(APIGatewayHttpApiV2ProxyRequest))]
[JsonSerializable(typeof(APIGatewayHttpApiV2ProxyResponse))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(Connection))]
[JsonSerializable(typeof(Dictionary<string, string>))]
public partial class CustomJsonSerializerContext : JsonSerializerContext
{
}
