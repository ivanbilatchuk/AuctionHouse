namespace Community.Infrastructure.Seeding;

public interface IDataSeeder
{
    Task SeedAsync(CancellationToken ct = default);
}
