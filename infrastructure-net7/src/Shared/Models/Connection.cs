namespace Shared.Models;

[Serializable]
public class Connection
{
    public Connection()
    {
        
    }

    public Connection(string? connectionId, string? userId)
    {
        this.connectionId = connectionId;
        this.userId = userId;
    }
    
    public string? connectionId { get; set; }
    public string? userId { get; set; }
}