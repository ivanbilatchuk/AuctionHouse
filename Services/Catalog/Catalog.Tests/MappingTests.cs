using AutoMapper;
using Catalog.Bll.Mapping;

namespace Catalog.Tests;

public sealed class MappingTests
{
    [Fact]
    public void AutoMapper_configuration_is_valid()
    {
        var configuration = new MapperConfiguration(cfg => cfg.AddMaps(typeof(ItemProfile).Assembly));

        configuration.AssertConfigurationIsValid();
    }
}
