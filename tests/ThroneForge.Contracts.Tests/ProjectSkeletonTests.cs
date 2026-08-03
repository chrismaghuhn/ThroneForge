using System.Reflection;
using Xunit;

namespace ThroneForge.Contracts.Tests;

public sealed class ProjectSkeletonTests
{
    [Fact]
    public void ContractsAssemblyIsDiscoverable()
    {
        Assert.Equal("ThroneForge.Contracts", Assembly.Load("ThroneForge.Contracts").GetName().Name);
    }
}

