using Catalog.Domain.Entities;
using Catalog.Domain.Exceptions;

namespace Catalog.Tests;

public sealed class ItemTests
{
    private static Item NewItem() => new(1, Guid.NewGuid(), "test-01", "Test item", "test-item", 10m, 20m);

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-5, 10)]
    [InlineData(30, 20)]
    public void Constructor_rejects_invalid_estimate(decimal low, decimal high)
        => Assert.Throws<DomainException>(() => new Item(1, Guid.NewGuid(), "SKU-1", "Title", "slug", low, high));

    [Fact]
    public void Constructor_normalizes_sku_and_starts_as_draft()
    {
        var item = NewItem();

        Assert.Equal("TEST-01", item.Sku);
        Assert.Equal(ItemStatus.Draft, item.Status);
    }

    [Fact]
    public void Status_follows_allowed_transitions()
    {
        var item = NewItem();

        item.List();
        item.SendToAuction();
        item.MarkSold();

        Assert.Equal(ItemStatus.Sold, item.Status);
        Assert.Throws<DomainException>(item.Withdraw);
    }

    [Fact]
    public void Draft_item_cannot_be_sold()
        => Assert.Throws<DomainException>(NewItem().MarkSold);

    [Fact]
    public void Same_tag_cannot_be_added_twice()
    {
        var item = NewItem();
        item.AddTag(3);

        Assert.Throws<DomainException>(() => item.AddTag(3));
        Assert.Single(item.ItemTags);
    }

    [Fact]
    public void First_image_becomes_primary_and_new_primary_replaces_it()
    {
        var item = NewItem();
        var first = item.AddImage("https://img.example.com/1.jpg");
        var second = item.AddImage("https://img.example.com/2.jpg", isPrimary: true);

        Assert.False(first.IsPrimary);
        Assert.True(second.IsPrimary);
        Assert.Single(item.Images, i => i.IsPrimary);
    }
}
