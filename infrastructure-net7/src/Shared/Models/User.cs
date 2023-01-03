namespace Shared.Models;

[Serializable]
public class User
{
    public User()
    {
        
    }
    public User(string username, Status status)
    {
        this.username = username;
        this.status = status;
    }
    public string? username { get; set; }
    public Status status { get; set; }
}