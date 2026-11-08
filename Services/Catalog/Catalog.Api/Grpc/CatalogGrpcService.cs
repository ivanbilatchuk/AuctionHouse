using System.Globalization;
using Catalog.Bll.DTO;
using Catalog.Bll.Services;
using Catalog.Domain.Exceptions;
using Catalog.Grpc;
using Grpc.Core;

namespace Catalog.Api.Grpc;

// Тонкий адаптер gRPC над IItemService (кешуючий декоратор: повторні читання йдуть з L1/L2).
// Очікувані помилки -> InvalidArgument / NotFound, а не Internal.
public sealed class CatalogGrpcService(IItemService items) : CatalogService.CatalogServiceBase
{
    private const int MaxBatchSize = 100;

    public override async Task<ItemReply> GetItem(GetItemRequest request, ServerCallContext context)
    {
        if (request.Id <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "id must be positive."));

        try
        {
            return ToReply(await items.GetByIdAsync(request.Id, context.CancellationToken));
        }
        catch (EntityNotFoundException ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
    }

    public override async Task<GetItemsByIdsReply> GetItemsByIds(GetItemsByIdsRequest request, ServerCallContext context)
    {
        if (request.Ids.Count is 0 or > MaxBatchSize)
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"ids must contain 1..{MaxBatchSize} values."));
        if (request.Ids.Any(id => id <= 0))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "All ids must be positive."));

        var reply = new GetItemsByIdsReply();
        foreach (var id in request.Ids.Distinct())
        {
            try
            {
                reply.Items.Add(ToReply(await items.GetByIdAsync(id, context.CancellationToken)));
            }
            catch (EntityNotFoundException)
            {
                reply.MissingIds.Add(id);
            }
        }

        return reply;
    }

    private static ItemReply ToReply(ItemDetailsDto item)
    {
        var reply = new ItemReply
        {
            Id = item.Id,
            Sku = item.Sku,
            Title = item.Title,
            CategoryName = item.CategoryName ?? "",
            EstimateLow = item.EstimateLow.ToString(CultureInfo.InvariantCulture),
            EstimateHigh = item.EstimateHigh.ToString(CultureInfo.InvariantCulture),
            Currency = item.Currency,
            Status = item.Status
        };

        if (item.Detail is { } d)
        {
            reply.Detail = new ItemDetail { Condition = d.Condition };
            if (d.ProductionYear is { } year) reply.Detail.ProductionYear = year;
            if (d.CountryOfOrigin is { } country) reply.Detail.CountryOfOrigin = country;
            if (d.Provenance is { } provenance) reply.Detail.Provenance = provenance;
            if (d.Description is { } description) reply.Detail.Description = description;
        }

        reply.Images.AddRange(item.Images.Select(i => new ItemImage { Url = i.Url, IsPrimary = i.IsPrimary }));
        return reply;
    }
}
