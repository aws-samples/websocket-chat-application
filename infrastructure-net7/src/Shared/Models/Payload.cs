namespace Shared.Models;

[Serializable]
public class Payload
{
    public string type { get; private set; }
    public Payload(string type)
    {
        this.type = type;
    }
}