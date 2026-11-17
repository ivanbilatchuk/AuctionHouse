using Catalog.Domain.Entities;
using Catalog.Domain.Exceptions;

namespace Catalog.Tests;

// Лаба 8: проєкція статусу аукціону (подія AuctionStatusChanged) на статус предмета.
public sealed class AuctionStatusProjectionTests
{
    private static readonly DateTime T0 = new(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc);

    private static Item ListedItem()
    {
        var item = new Item(1, Guid.NewGuid(), "test-01", "Test item", "test-item", 10m, 20m);
        item.List();
        return item;
    }

    [Fact]
    public void Full_auction_lifecycle_is_projected()
    {
        var item = ListedItem();

        Assert.True(item.ApplyAuctionStatus("Active", T0));
        Assert.Equal(ItemStatus.InAuction, item.Status);

        Assert.True(item.ApplyAuctionStatus("Closed", T0.AddHours(1)));
        Assert.Equal(ItemStatus.Sold, item.Status);
        Assert.Equal(T0.AddHours(1), item.AuctionStatusSyncedAt);
    }

    [Theory]
    [InlineData("Unsold")]
    [InlineData("Cancelled")]
    public void Failed_auction_returns_item_to_listing(string auctionStatus)
    {
        var item = ListedItem();
        item.ApplyAuctionStatus("Active", T0);

        Assert.True(item.ApplyAuctionStatus(auctionStatus, T0.AddMinutes(5)));
        Assert.Equal(ItemStatus.Listed, item.Status);
    }

    [Fact]
    public void Stale_event_is_ignored_out_of_order()
    {
        var item = ListedItem();
        item.ApplyAuctionStatus("Active", T0);
        item.ApplyAuctionStatus("Closed", T0.AddHours(1));

        // Cancelled стався раніше за Closed, але прийшов пізніше — не перезаписує новіший стан.
        Assert.False(item.ApplyAuctionStatus("Cancelled", T0.AddMinutes(30)));
        Assert.Equal(ItemStatus.Sold, item.Status);
    }

    [Fact]
    public void Same_event_applied_twice_does_not_change_state()
    {
        var item = ListedItem();
        item.ApplyAuctionStatus("Active", T0);

        Assert.False(item.ApplyAuctionStatus("Active", T0));
        Assert.Equal(ItemStatus.InAuction, item.Status);
    }

    [Fact]
    public void Cancelled_auction_of_item_that_never_went_to_auction_changes_nothing()
    {
        var draft = new Item(1, Guid.NewGuid(), "test-02", "Draft item", "draft-item", 10m, 20m);

        Assert.True(draft.ApplyAuctionStatus("Cancelled", T0));
        Assert.Equal(ItemStatus.Draft, draft.Status);
        Assert.Equal(T0, draft.AuctionStatusSyncedAt);
    }

    [Fact]
    public void Impossible_transition_is_a_permanent_failure()
    {
        var draft = new Item(1, Guid.NewGuid(), "test-03", "Draft item", "draft-item-3", 10m, 20m);

        // Аукціон активовано для предмета в Draft — порушення бізнес-правила, повідомлення піде в DLQ.
        Assert.Throws<DomainException>(() => draft.ApplyAuctionStatus("Active", T0));
    }

    [Fact]
    public void Unknown_auction_status_is_rejected()
        => Assert.Throws<DomainException>(() => ListedItem().ApplyAuctionStatus("Scheduled", T0));
}
