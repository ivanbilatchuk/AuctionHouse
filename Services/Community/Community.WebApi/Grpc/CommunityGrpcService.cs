using Community.Application.Queries.ExpertOpinions.GetExpertOpinions;
using Community.Grpc;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using MediatR;
using MongoDB.Driver;
using ValidationException = Community.Application.Common.ValidationException;

namespace Community.WebApi.Grpc;

// Тонкий адаптер gRPC над MediatR: той самий GetExpertOpinionsQuery, що й REST, тож працюють
// валідація, логування й кешування (CachingBehavior). Очікувані помилки -> InvalidArgument / Unavailable.
public sealed class CommunityGrpcService(ISender sender) : CommunityService.CommunityServiceBase
{
    private const int DefaultPageSize = 20;

    public override async Task<GetOpinionsForItemReply> GetOpinionsForItem(GetOpinionsForItemRequest request, ServerCallContext context)
    {
        if (request.ItemId <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "item_id must be positive."));
        if (request.PageSize is < 0 or > GetExpertOpinionsQuery.MaxPageSize)
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"page_size must be in 0..{GetExpertOpinionsQuery.MaxPageSize}."));

        var query = new GetExpertOpinionsQuery
        {
            ItemId = request.ItemId,
            Page = 1,
            PageSize = request.PageSize == 0 ? DefaultPageSize : request.PageSize
        };

        try
        {
            var page = await sender.Send(query, context.CancellationToken);

            var reply = new GetOpinionsForItemReply { TotalCount = page.TotalCount };
            reply.Opinions.AddRange(page.Items.Select(o => new ExpertOpinion
            {
                Id = o.Id,
                ItemId = o.ItemId,
                CategoryKind = o.CategoryKind,
                ExpertName = o.ExpertName,
                Verdict = o.Verdict,
                Confidence = o.Confidence,
                CreatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(o.CreatedAt, DateTimeKind.Utc))
            }));

            return reply;
        }
        catch (ValidationException ex)
        {
            var detail = string.Join("; ", ex.Errors.Select(e => $"{e.Key}: {string.Join(", ", e.Value)}"));
            throw new RpcException(new Status(StatusCode.InvalidArgument, detail));
        }
        catch (Exception ex) when (ex is MongoConnectionException or TimeoutException)
        {
            throw new RpcException(new Status(StatusCode.Unavailable, "MongoDB is temporarily unavailable."));
        }
    }
}
