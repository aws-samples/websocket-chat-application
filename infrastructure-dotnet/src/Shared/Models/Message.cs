namespace Shared.Models;

[Serializable]
public class Message : Payload
{
    public Message() : base("Message") {
    }

    public Message(string type, string sender, string text, DateTime sentAt, string channelId, string messageId) : base(type)
    {
        this.sender = sender;
        this.text = text;
        this.sentAt = sentAt;
        this.channelId = channelId;
        this.messageId = messageId;
    }

    public string? sender { get; set; }
    public string? text { get; set; }
    public DateTime? sentAt { get; set; }
    public string? channelId { get; set; }
    public string? messageId { get; set; }
}