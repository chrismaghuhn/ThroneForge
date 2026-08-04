using ThroneForge.Contracts;

namespace ThroneForge.API;

public interface IThroneForgeMod
{
    ValueTask InitializeAsync(IModContext context, CancellationToken cancellationToken);

    ValueTask ShutdownAsync(CancellationToken cancellationToken);
}

public interface IModContext
{
    ModIdentity Identity { get; }

    ICapabilityService Capabilities { get; }
}

public interface ICapabilityService
{
    bool IsAvailable(string capabilityKey);
}
