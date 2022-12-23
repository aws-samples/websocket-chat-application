namespace Shared.Models;

public class Channel
{
    public string? id { get; set; }
    public User[] Participants { get; set; } = null!;
}