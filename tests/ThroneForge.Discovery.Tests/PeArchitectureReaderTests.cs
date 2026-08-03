using ThroneForge.Discovery;
using Xunit;

namespace ThroneForge.Discovery.Tests;

public sealed class PeArchitectureReaderTests
{
    [Theory]
    [InlineData(0x014C, ExecutableArchitecture.X86)]
    [InlineData(0x8664, ExecutableArchitecture.X64)]
    [InlineData(0xAA64, ExecutableArchitecture.Arm64)]
    public void ReadsSupportedPeMachineTypes(ushort machine, ExecutableArchitecture expected)
    {
        using var fixture = new DiscoveryTestFixture();
        var executable = Path.Combine(fixture.Root, "synthetic.exe");
        DiscoveryTestFixture.WriteMinimalPe(executable, machine);

        Assert.True(PeArchitectureReader.TryRead(executable, out var actual));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void RejectsMalformedPeHeader()
    {
        using var fixture = new DiscoveryTestFixture();
        var executable = Path.Combine(fixture.Root, "malformed.exe");
        File.WriteAllBytes(executable, [0x4D, 0x5A, 0x00]);

        Assert.False(PeArchitectureReader.TryRead(executable, out var actual));
        Assert.Equal(ExecutableArchitecture.Unknown, actual);
    }
}
