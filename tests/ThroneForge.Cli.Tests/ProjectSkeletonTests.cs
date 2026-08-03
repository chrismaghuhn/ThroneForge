using System.Reflection;
using Xunit;

namespace ThroneForge.Cli.Tests;

public sealed class ProjectSkeletonTests
{
    [Fact]
    public void CliAssemblyIsDiscoverable()
    {
        Assert.Equal("ThroneForge.Cli", Assembly.Load("ThroneForge.Cli").GetName().Name);
    }
}

