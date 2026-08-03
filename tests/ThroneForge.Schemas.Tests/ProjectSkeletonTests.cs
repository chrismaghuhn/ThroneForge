using System.Reflection;
using Xunit;

namespace ThroneForge.Schemas.Tests;

public sealed class ProjectSkeletonTests
{
    [Fact]
    public void SchemasAssemblyIsDiscoverable()
    {
        Assert.Equal("ThroneForge.Schemas", Assembly.Load("ThroneForge.Schemas").GetName().Name);
    }
}

