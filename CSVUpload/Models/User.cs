namespace CSVUpload.Models;

public class User
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? About { get; set; }
    public string? Number { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
