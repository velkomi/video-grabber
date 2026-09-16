namespace VideoGrabber.Core.Security;

public sealed record EgressPolicy(string Name, bool PublicOnly);

public sealed record EgressSessionLease(Guid Id, Uri ProxyUri, EgressPolicy Policy);

public interface IManagedEgressSessionResolver
{
    EgressSessionLease Resolve(Guid id, Uri exactEndpoint);
}

public interface IManagedEgressSessionRegistry : IManagedEgressSessionResolver
{
    EgressSessionLease Issue(Uri proxyUri, EgressPolicy policy);
    void Revoke(Guid id);
}
