namespace Shared.Models;

public class Message : Payload
{
    public Message(string type) : base(type)
    {
    }

    public string sender { get; set; }
    public string text { get; set; }
    public DateTime sentAt { get; set; }
    public string channelId { get; set; }
    public string messageId { get; set; }
}