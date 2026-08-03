using System.Reflection;
using Xunit;

namespace ThroneForge.Runtime.Tests;

public sealed class ProjectSkeletonTests
{
    [Fact]
    public void RuntimeAssemblyIsDiscoverable()
    {
        Assert.Equal("ThroneForge.Runtime", Assembly.Load("ThroneForge.Runtime").GetName().Name);
    }
}

