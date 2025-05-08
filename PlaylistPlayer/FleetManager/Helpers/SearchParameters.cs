// FleetManager/Helpers/SearchParameters.cs
namespace FleetManager.Helpers;

public class SearchParameters
{
    private int _pageNumber = 1;

    // Increase default page size to something more reasonable
    private int _pageSize = 10; // Changed from 2 to 10

    private const int MaxPageSize = 50;

    public int? PageNumber
    {
        get => _pageNumber;
        // Ensure value is at least 1
        set => _pageNumber = (value.HasValue && value.Value > 0) ? value.Value : 1;
    }

    public int? PageSize
    {
        // Ensure value is at least 1 and not more than MaxPageSize
        get => _pageSize > MaxPageSize ? MaxPageSize : _pageSize;
        set =>
            _pageSize =
                (value.HasValue && value.Value > 0)
                    ? Math.Min(value.Value, MaxPageSize)
                    : _pageSize;
    }
}
