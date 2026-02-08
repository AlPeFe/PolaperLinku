using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using PolaperLinku.Api.Models;
using PolaperLinku.Api.Services;

namespace PolaperLinku.Api.Extensions;

public static class EndpointExtensions
{
    public static IEndpointRouteBuilder MapAppEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        var folders = api.MapGroup("/folders");
        folders.MapGet("/", GetFolders);
        folders.MapPost("/", CreateFolder);
        folders.MapDelete("/{id:int}", DeleteFolder);

        var favorites = api.MapGroup("/favorites");
        favorites.MapGet("/", GetFavorites);
        favorites.MapPost("/", CreateFavorite);
        favorites.MapPut("/{id:int}", UpdateFavorite);
        favorites.MapDelete("/{id:int}", DeleteFavorite);
        favorites.MapGet("/export", ExportFavorites);

        api.MapGet("/metadata/clear", ClearMetadataCache);

        return app;
    }

    private static async Task<IResult> GetFolders(AppDbContext db)
    {
        var folders = await db.Folders.OrderBy(f => f.Name).ToListAsync();
        return Results.Ok(folders);
    }

    private static async Task<IResult> CreateFolder([FromBody] Folder folder, AppDbContext db)
    {
        folder.CreatedAt = DateTime.UtcNow;
        db.Folders.Add(folder);
        await db.SaveChangesAsync();
        return Results.Created($"/api/folders/{folder.Id}", folder);
    }

    private static async Task<IResult> DeleteFolder(int id, AppDbContext db)
    {
        var folder = await db.Folders.FindAsync(id);
        if (folder is null) return Results.NotFound();

        db.Folders.Remove(folder);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> GetFavorites(AppDbContext db, [FromQuery] int? folderId, [FromQuery] string? orderBy)
    {
        var query = db.Favorites.AsQueryable();

        if (folderId.HasValue)
        {
            if (folderId.Value == 0)
            {
                query = query.Where(f => f.FolderId == null);
            }
            else
            {
                query = query.Where(f => f.FolderId == folderId.Value);
            }
        }

        query = orderBy?.ToLower() switch
        {
            "date" => query.OrderByDescending(f => f.CreatedAt),
            "title" => query.OrderBy(f => f.Title),
            _ => query.OrderByDescending(f => f.CreatedAt)
        };

        var favorites = await query.Include(f => f.Folder).ToListAsync();
        return Results.Ok(favorites);
    }

    private static async Task<IResult> CreateFavorite([FromBody] CreateFavoriteRequest request, AppDbContext db, MetadataExtractor metadataExtractor)
    {
        var (title, description, imageUrl) = await metadataExtractor.ExtractMetadataAsync(request.Url);

        var favorite = new Favorite
        {
            Title = title,
            Url = request.Url,
            Description = description ?? string.Empty,
            FolderId = request.FolderId,
            PreviewImage = imageUrl,
            CreatedAt = DateTime.UtcNow
        };

        db.Favorites.Add(favorite);
        await db.SaveChangesAsync();
        return Results.Created($"/api/favorites/{favorite.Id}", favorite);
    }

    private static async Task<IResult> UpdateFavorite(int id, [FromBody] Favorite favorite, AppDbContext db)
    {
        var existing = await db.Favorites.FindAsync(id);
        if (existing is null) return Results.NotFound();

        existing.Title = favorite.Title;
        existing.Url = favorite.Url;
        existing.Description = favorite.Description;
        existing.FolderId = favorite.FolderId;
        existing.PreviewImage = favorite.PreviewImage;

        await db.SaveChangesAsync();
        return Results.Ok(existing);
    }

    private static async Task<IResult> DeleteFavorite(int id, AppDbContext db)
    {
        var favorite = await db.Favorites.FindAsync(id);
        if (favorite is null) return Results.NotFound();

        db.Favorites.Remove(favorite);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> ExportFavorites(AppDbContext db)
    {
        var favorites = await db.Favorites.Include(f => f.Folder).ToListAsync();
        return Results.Ok(favorites);
    }

    private static IResult ClearMetadataCache(MetadataCache cache)
    {
        cache.Clear();
        return Results.Ok(new { message = "Metadata cache cleared" });
    }
}

public class CreateFavoriteRequest
{
    public string Url { get; set; } = string.Empty;
    public int? FolderId { get; set; }
}
