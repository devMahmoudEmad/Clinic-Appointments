namespace Clinic.Models.Dtos
{
    /// <summary>
    /// Page of items for a server-side paged grid, together with the paging
    /// metadata needed to render pagination controls.
    /// </summary>
    public sealed class PagedResult<T>
    {
        public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();

        public int CurrentPage { get; init; }

        public int PageSize { get; init; }

        public int TotalCount { get; init; }

        public int TotalPages { get; init; }

        public static PagedResult<T> Create(IReadOnlyList<T> items, int page, int pageSize, int totalCount)
        {
            var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
            return new PagedResult<T>
            {
                Items = items,
                CurrentPage = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages
            };
        }
    }

    /// <summary>
    /// Minimal contract used by the shared pagination partial so any grid
    /// view model can render the same pagination controls.
    /// </summary>
    public interface IPagedViewModel
    {
        int CurrentPage { get; }

        int TotalPages { get; }
    }
}
