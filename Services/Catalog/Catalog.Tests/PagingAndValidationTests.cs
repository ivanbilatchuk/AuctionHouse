using Catalog.Bll.DTO;
using Catalog.Bll.Specifications;
using Catalog.Bll.Validation;

namespace Catalog.Tests;

public sealed class PagingAndValidationTests
{
    [Fact]
    public void PagedResult_computes_metadata_from_total_count()
    {
        var page = new PagedResult<int>([1, 2, 3, 4, 5], TotalCount: 12, Page: 2, PageSize: 5);

        Assert.Equal(3, page.TotalPages);
        Assert.True(page.HasNext);
        Assert.True(page.HasPrevious);
    }

    [Theory]
    [InlineData(0, 500, 1, 100)]
    [InlineData(3, 0, 3, 1)]
    public void Paging_is_clamped(int page, int pageSize, int expectedPage, int expectedSize)
        => Assert.Equal((expectedPage, expectedSize), Paging.Normalize(page, pageSize));

    [Fact]
    public void Query_rejects_sort_by_unknown_field()
    {
        var result = new ItemQueryValidator().Validate(new ItemQuery { SortBy = "password" });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(ItemQuery.SortBy));
    }

    [Fact]
    public void Create_item_validator_reports_field_errors()
    {
        var dto = new CreateItemDto { Sku = "bad sku", Slug = "Bad Slug", EstimateLow = 10, EstimateHigh = 5, Currency = "usd" };

        var result = new CreateItemDtoValidator().Validate(dto);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateItemDto.Sku));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateItemDto.Slug));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateItemDto.EstimateHigh));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateItemDto.Currency));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateItemDto.SellerExternalId));
    }

    [Fact]
    public void Filter_spec_applies_paging_in_query()
    {
        var spec = new ItemsFilterSpec(new ItemQuery(), page: 2, pageSize: 5);

        Assert.Equal(5, spec.Skip);
        Assert.Equal(5, spec.Take);
    }
}
