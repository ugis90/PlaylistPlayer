namespace FleetManager.Helpers;

public record PaginationMetadata(
    int TotalCount,
    int PageSize,
    int CurrentPage,
    int TotalPages,
    string? PreviousPageLink,
    string? NextPageLink
);
