using Catalog.Bll.DTO;

namespace Catalog.Api.Controllers;

internal static class PaginationHeaders
{
    public static void Write<T>(HttpResponse response, PagedResult<T> page)
    {
        response.Headers["X-Total-Count"] = page.TotalCount.ToString();
        response.Headers["X-Total-Pages"] = page.TotalPages.ToString();
        response.Headers["X-Page"] = page.Page.ToString();
        response.Headers["X-Page-Size"] = page.PageSize.ToString();
    }
}
