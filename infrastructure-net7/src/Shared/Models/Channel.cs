namespace Shared.Models;

[Serializable]
public class Channel
{
    public Channel()
    {
        
    }
    public Channel(string id, User[] participants)
    {
        this.id = id;
        this.Participants = participants;
    }
    public string? id { get; set; }
    public User[] Participants { get; set; }
}