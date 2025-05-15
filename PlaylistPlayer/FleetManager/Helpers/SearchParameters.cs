namespace FleetManager.Helpers;

public class SearchParameters
{
    private int _pageNumber = 1;

    private int _pageSize = 10;

    private const int MaxPageSize = 50;

    public int? PageNumber
    {
        get => _pageNumber;
        set => _pageNumber = (value.HasValue && value.Value > 0) ? value.Value : 1;
    }

    public int? PageSize
    {
        get => _pageSize > MaxPageSize ? MaxPageSize : _pageSize;
        set =>
            _pageSize =
                (value.HasValue && value.Value > 0)
                    ? Math.Min(value.Value, MaxPageSize)
                    : _pageSize;
    }
}
