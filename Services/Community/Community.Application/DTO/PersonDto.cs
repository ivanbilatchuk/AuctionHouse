namespace Community.Application.DTO;

public sealed class PersonDto
{
    public Guid UserId { get; init; }
    public string DisplayName { get; init; } = "";
}
