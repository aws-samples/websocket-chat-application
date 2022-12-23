namespace Shared.Models;

public class StatusChangeEvent : Payload
{
    public StatusChangeEvent(string type) : base(type)
    {
    }

    public string userId { get; set; }
    public Status currentStatus { get; set; }
    public DateTime eventDate { get; set; }
}