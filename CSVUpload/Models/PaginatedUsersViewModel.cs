using CSVUpload.Models;
using System.Collections.Generic;

public class PaginatedUsersViewModel
{
    public IEnumerable<User> Users { get; set; } = new List<User>();
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalCount { get; set; }

    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}
