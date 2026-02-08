namespace PolaperLinku.Api.Models;

public class Favorite
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? FolderId { get; set; }
    public Folder? Folder { get; set; }
    public string? PreviewImage { get; set; }
}

public class Folder
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
}
