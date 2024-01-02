namespace Shared.Models;

public class StatusChangeEvent : Payload
{
    public StatusChangeEvent() : base("StatusChangeEvent")
    {
    }

    public StatusChangeEvent(string userId, Status currentStatus, DateTime eventDate) : base("StatusChangeEvent")
    {
        this.userId = userId;
        this.currentStatus = currentStatus;
        this.eventDate = eventDate;
    }

    public string? userId { get; set; }
    public Status? currentStatus { get; set; }
    public DateTime? eventDate { get; set; }
}