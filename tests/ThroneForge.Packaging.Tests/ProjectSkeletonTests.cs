using System.Reflection;
using Xunit;

namespace ThroneForge.Packaging.Tests;

public sealed class ProjectSkeletonTests
{
    [Fact]
    public void PackagingAssemblyIsDiscoverable()
    {
        Assert.Equal("ThroneForge.Packaging", Assembly.Load("ThroneForge.Packaging").GetName().Name);
    }
}

