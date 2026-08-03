using System.Reflection;
using Xunit;

namespace ThroneForge.GameAdapter.Tests;

public sealed class ProjectSkeletonTests
{
    [Fact]
    public void AdapterAssemblyIsDiscoverable()
    {
        Assert.Equal("ThroneForge.GameAdapter.Thronefall", Assembly.Load("ThroneForge.GameAdapter.Thronefall").GetName().Name);
    }
}

