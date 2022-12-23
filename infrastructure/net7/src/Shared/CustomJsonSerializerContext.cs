using System.Text.Json.Serialization;
using Amazon.Lambda.APIGatewayEvents;

namespace Shared;

[JsonSerializable(typeof(APIGatewayHttpApiV2ProxyRequest))]
[JsonSerializable(typeof(APIGatewayHttpApiV2ProxyResponse))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
public partial class CustomJsonSerializerContext : JsonSerializerContext
{
}
