using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlaylistPlayer.Data;
using PlaylistPlayer.Data.Entities;
using PlaylistPlayer.Helpers;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Extensions;

namespace PlaylistPlayer
{
    public static class Endpoints
    {
        public static void AddCategoryApi(this WebApplication app)
        {
            var categoriesGroup = app.MapGroup("/api").AddFluentValidationAutoValidation();

            categoriesGroup
                .MapGet(
                    "/categories",
                    async (
                        [AsParameters] SearchParameters searchParams,
                        LinkGenerator linkGenerator,
                        HttpContext httpContext,
                        MusicDbContext dbContext
                    ) =>
                    {
                        var queryable = dbContext.Categories
                            .AsQueryable()
                            .OrderBy(o => o.CreatedAt);

                        var pagedList = await PagedList<Category>.CreateAsync(
                            queryable,
                            searchParams.PageNumber!.Value,
                            searchParams.PageSize!.Value
                        );

                        var resources = pagedList
                            .Select(category =>
                            {
                                var links = CreateLinksForSingleCategory(
                                        category.Id,
                                        linkGenerator,
                                        httpContext
                                    )
                                    .ToArray();
                                return new ResourceDto<CategoryDto>(category.ToDto(), links);
                            })
                            .ToArray();

                        var links = CreateLinksForCategories(
                                linkGenerator,
                                httpContext,
                                pagedList.GetPreviousPageLink(
                                    linkGenerator,
                                    httpContext,
                                    "GetCategories"
                                ),
                                pagedList.GetNextPageLink(
                                    linkGenerator,
                                    httpContext,
                                    "GetCategories"
                                )
                            )
                            .ToArray();

                        var paginationMetadata = pagedList.CreatePaginationMetadata(
                            linkGenerator,
                            httpContext,
                            "GetCategories"
                        );
                        httpContext.Response.Headers.Append(
                            "Pagination",
                            JsonSerializer.Serialize(paginationMetadata)
                        );

                        return new ResourceDto<ResourceDto<CategoryDto>[]>(resources, links);
                    }
                )
                .WithName("GetCategories");

            categoriesGroup
                .MapGet(
                    "/categories/{categoryId}",
                    async (int categoryId, MusicDbContext dbContext) =>
                    {
                        var category = await dbContext.Categories.FindAsync(categoryId);
                        return category == null ? Results.NotFound() : Results.Ok(category.ToDto());
                    }
                )
                .WithName("GetCategory")
                .AddEndpointFilter<ETagFilter>();

            categoriesGroup
                .MapPost(
                    "/categories",
                    async (
                        CreateCategoryDto dto,
                        LinkGenerator linkGenerator,
                        HttpContext httpContext,
                        MusicDbContext dbContext
                    ) =>
                    {
                        var category = new Category
                        {
                            Name = dto.Name,
                            Description = dto.Description,
                            CreatedAt = DateTimeOffset.UtcNow
                        };
                        dbContext.Categories.Add(category);

                        await dbContext.SaveChangesAsync();

                        var links = CreateLinksForSingleCategory(
                                category.Id,
                                linkGenerator,
                                httpContext
                            )
                            .ToArray();
                        var categoryDto = category.ToDto();
                        var resource = new ResourceDto<CategoryDto>(categoryDto, links);

                        return TypedResults.Created(links[0].Href, resource);
                    }
                )
                .WithName("CreateCategory");

            categoriesGroup
                .MapPut(
                    "/categories/{categoryId}",
                    async (UpdateCategoryDto dto, int categoryId, MusicDbContext dbContext) =>
                    {
                        var category = await dbContext.Categories.FindAsync(categoryId);
                        if (category == null)
                        {
                            return Results.NotFound();
                        }

                        category.Description = dto.Description;

                        dbContext.Categories.Update(category);
                        await dbContext.SaveChangesAsync();

                        return Results.Ok(category.ToDto());
                    }
                )
                .WithName("UpdateCategory");

            categoriesGroup
                .MapDelete(
                    "/categories/{categoryId}",
                    async (int categoryId, MusicDbContext dbContext) =>
                    {
                        var category = await dbContext.Categories.FindAsync(categoryId);
                        if (category == null)
                        {
                            return Results.NotFound();
                        }

                        dbContext.Categories.Remove(category);
                        await dbContext.SaveChangesAsync();

                        return Results.NoContent();
                    }
                )
                .WithName("RemoveCategory");
        }

        public static void AddPlaylistApi(this WebApplication app)
        {
            var playlistsGroup = app.MapGroup("/api/categories/{categoryId:int}/playlists")
                .AddFluentValidationAutoValidation();

            playlistsGroup
                .MapGet(
                    "/",
                    async ([FromRoute] int categoryId, MusicDbContext dbContext) =>
                    {
                        var playlists = await dbContext.Playlists
                            .Where(p => p.CategoryId == categoryId)
                            .Select(p => p.ToDto())
                            .ToListAsync();
                        return TypedResults.Ok(playlists);
                    }
                )
                .WithName("GetPlaylists");

            playlistsGroup
                .MapGet(
                    "/{playlistId}",
                    async (int categoryId, int playlistId, MusicDbContext dbContext) =>
                    {
                        var playlist = await dbContext.Playlists.FirstOrDefaultAsync(
                            p => p.CategoryId == categoryId && p.Id == playlistId
                        );
                        return playlist == null
                            ? Results.NotFound()
                            : TypedResults.Ok(playlist.ToDto());
                    }
                )
                .WithName("GetPlaylist");

            playlistsGroup
                .MapPost(
                    "/",
                    async (
                        [FromRoute] int categoryId,
                        [FromBody] CreatePlaylistDto dto,
                        MusicDbContext dbContext
                    ) =>
                    {
                        var category = await dbContext.Categories.FindAsync(categoryId);
                        if (category == null)
                            return Results.NotFound("Category not found");

                        var playlist = new Playlist
                        {
                            Name = dto.Name,
                            Description = dto.Description,
                            CategoryId = categoryId,
                            CreatedAt = DateTimeOffset.UtcNow
                        };
                        dbContext.Playlists.Add(playlist);
                        await dbContext.SaveChangesAsync();

                        return TypedResults.Created(
                            $"/api/categories/{categoryId}/playlists/{playlist.Id}",
                            playlist.ToDto()
                        );
                    }
                )
                .WithName("CreatePlaylist");

            playlistsGroup
                .MapPut(
                    "/{playlistId}",
                    async (
                        int categoryId,
                        int playlistId,
                        UpdatePlaylistDto dto,
                        MusicDbContext dbContext
                    ) =>
                    {
                        var playlist = await dbContext.Playlists.FirstOrDefaultAsync(
                            p => p.CategoryId == categoryId && p.Id == playlistId
                        );
                        if (playlist == null)
                            return Results.NotFound();

                        playlist.Name = dto.Name;
                        playlist.Description = dto.Description;
                        await dbContext.SaveChangesAsync();

                        return TypedResults.Ok(playlist.ToDto());
                    }
                )
                .WithName("UpdatePlaylist");

            playlistsGroup
                .MapDelete(
                    "/{playlistId}",
                    async (int categoryId, int playlistId, MusicDbContext dbContext) =>
                    {
                        var playlist = await dbContext.Playlists.FirstOrDefaultAsync(
                            p => p.CategoryId == categoryId && p.Id == playlistId
                        );
                        if (playlist == null)
                            return Results.NotFound();

                        dbContext.Playlists.Remove(playlist);
                        await dbContext.SaveChangesAsync();

                        return Results.NoContent();
                    }
                )
                .WithName("DeletePlaylist");
        }

        public static void AddSongApi(this WebApplication app)
        {
            var songsGroup = app.MapGroup(
                    "/api/categories/{categoryId}/playlists/{playlistId}/songs"
                )
                .AddFluentValidationAutoValidation();

            songsGroup
                .MapGet(
                    "/",
                    async (int categoryId, int playlistId, MusicDbContext dbContext) =>
                    {
                        var songs = await dbContext.Songs
                            .Where(
                                s =>
                                    s.Playlist.CategoryId == categoryId
                                    && s.PlaylistId == playlistId
                            )
                            .Select(s => s.ToDto())
                            .ToListAsync();
                        return TypedResults.Ok(songs);
                    }
                )
                .WithName("GetSongs");

            songsGroup
                .MapGet(
                    "/{songId}",
                    async (int categoryId, int playlistId, int songId, MusicDbContext dbContext) =>
                    {
                        var song = await dbContext.Songs.FirstOrDefaultAsync(
                            s =>
                                s.Playlist.CategoryId == categoryId
                                && s.PlaylistId == playlistId
                                && s.Id == songId
                        );
                        return song == null ? Results.NotFound() : TypedResults.Ok(song.ToDto());
                    }
                )
                .WithName("GetSong");

            songsGroup
                .MapPost(
                    "/",
                    async (
                        int categoryId,
                        int playlistId,
                        CreateSongDto dto,
                        MusicDbContext dbContext
                    ) =>
                    {
                        var playlist = await dbContext.Playlists
                            .Include(p => p.Songs)
                            .FirstOrDefaultAsync(
                                p => p.CategoryId == categoryId && p.Id == playlistId
                            );

                        if (playlist == null)
                            return Results.NotFound("Playlist not found");

                        var newOrderId =
                            playlist.Songs.Count > 0 ? playlist.Songs.Max(s => s.OrderId) + 1 : 1;

                        var song = new Song
                        {
                            Title = dto.Title,
                            Artist = dto.Artist,
                            Duration = dto.Duration,
                            PlaylistId = playlistId,
                            CreatedAt = DateTimeOffset.UtcNow,
                            OrderId = newOrderId
                        };

                        dbContext.Songs.Add(song);
                        await dbContext.SaveChangesAsync();

                        return TypedResults.Created(
                            $"/api/categories/{categoryId}/playlists/{playlistId}/songs/{song.Id}",
                            song.ToDto()
                        );
                    }
                )
                .WithName("CreateSong");

            songsGroup
                .MapPut(
                    "/{songId}",
                    async (
                        int categoryId,
                        int playlistId,
                        int songId,
                        UpdateSongDto dto,
                        MusicDbContext dbContext
                    ) =>
                    {
                        var song = await dbContext.Songs.FirstOrDefaultAsync(
                            s =>
                                s.Playlist.CategoryId == categoryId
                                && s.PlaylistId == playlistId
                                && s.Id == songId
                        );

                        if (song == null)
                            return Results.NotFound();

                        // Update song properties
                        song.Title = dto.Title;
                        song.Artist = dto.Artist;
                        song.Duration = dto.Duration;

                        // Handle OrderId change
                        if (song.OrderId != dto.OrderId)
                        {
                            var playlistSongs = await dbContext.Songs
                                .Where(s => s.PlaylistId == playlistId)
                                .OrderBy(s => s.OrderId)
                                .ToListAsync();

                            // Remove the song from its current position
                            playlistSongs.Remove(song);

                            // Insert the song at the new position
                            int newIndex = Math.Min(dto.OrderId - 1, playlistSongs.Count);
                            playlistSongs.Insert(newIndex, song);

                            // Update OrderId for all songs in the playlist
                            for (int i = 0; i < playlistSongs.Count; i++)
                            {
                                playlistSongs[i].OrderId = i + 1;
                            }
                        }

                        await dbContext.SaveChangesAsync();

                        return TypedResults.Ok(song.ToDto());
                    }
                )
                .WithName("UpdateSong");

            songsGroup
                .MapDelete(
                    "/{songId}",
                    async (int categoryId, int playlistId, int songId, MusicDbContext dbContext) =>
                    {
                        var song = await dbContext.Songs.FirstOrDefaultAsync(
                            s =>
                                s.Playlist.CategoryId == categoryId
                                && s.PlaylistId == playlistId
                                && s.Id == songId
                        );
                        if (song == null)
                            return Results.NotFound();

                        dbContext.Songs.Remove(song);
                        await dbContext.SaveChangesAsync();

                        return Results.NoContent();
                    }
                )
                .WithName("DeleteSong");
        }

        private static IEnumerable<LinkDto> CreateLinksForSingleCategory(
            int categoryId,
            LinkGenerator linkGenerator,
            HttpContext httpContext
        )
        {
            yield return new LinkDto(
                linkGenerator.GetUriByName(httpContext, "GetCategory", new { categoryId }),
                "self",
                "GET"
            );
            yield return new LinkDto(
                linkGenerator.GetUriByName(httpContext, "UpdateCategory", new { categoryId }),
                "edit",
                "PUT"
            );
            yield return new LinkDto(
                linkGenerator.GetUriByName(httpContext, "RemoveCategory", new { categoryId }),
                "remove",
                "DELETE"
            );
            yield return new LinkDto(
                linkGenerator.GetUriByName(httpContext, "GetPlaylists", new { categoryId }),
                "playlists",
                "GET"
            );
        }

        private static IEnumerable<LinkDto> CreateLinksForCategories(
            LinkGenerator linkGenerator,
            HttpContext httpContext,
            string? previousPageLink,
            string? nextPageLink
        )
        {
            yield return new LinkDto(
                linkGenerator.GetUriByName(httpContext, "GetCategories"),
                "self",
                "GET"
            );

            if (previousPageLink != null)
                yield return new LinkDto(previousPageLink, "previousPage", "GET");

            if (nextPageLink != null)
                yield return new LinkDto(nextPageLink, "nextPage", "GET");
        }
    }
}
