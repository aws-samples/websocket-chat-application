namespace Shared.Models;

[Serializable]
public class WebsocketPayload
{
    public WebsocketPayload()
    {
        
    }

    public string? action { get; set; }
    public object? data { get; set; }
    
}