using System.Reflection;
using Xunit;

namespace ThroneForge.Logic.Tests;

public sealed class ProjectSkeletonTests
{
    [Fact]
    public void LogicAssemblyIsDiscoverable()
    {
        Assert.Equal("ThroneForge.Logic", Assembly.Load("ThroneForge.Logic").GetName().Name);
    }
}

