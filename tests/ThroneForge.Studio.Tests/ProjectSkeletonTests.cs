using System.Reflection;
using Xunit;

namespace ThroneForge.Studio.Tests;

public sealed class ProjectSkeletonTests
{
    [Fact]
    public void StudioAssemblyIsDiscoverable()
    {
        Assert.Equal("ThroneForge.Studio", Assembly.Load("ThroneForge.Studio").GetName().Name);
    }
}

