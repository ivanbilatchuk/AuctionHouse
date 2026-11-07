using MediatR;

namespace Community.Application.Abstractions.Messaging;

public interface IQuery<TResponse> : IRequest<TResponse>
{
}
