using System;
using System.Collections.Generic;
using Modl.Internal.Utils.AWS;

// IAWSClient provides the basic functionality provided by an AWS client.
public interface IAWSClient
{
    
    IAWSRequest GETRequest(string action, SortedDictionary<string, string> parameters);
    IAWSRequest POSTRequest(string action, string body);

}

// IAWSRequest provides the basic functionality for an AWS request:
// - packages the actual request;
// - offers a completed event for getting a call back once the request has gone through;
// - exposes the SendRequest method to actually use the request.
public abstract class IAWSRequest
{
    public event Action<IAWSRequest> completed;
    public abstract void SendRequest();

    protected void TriggerCompleted()
    {
        completed?.Invoke(this);
    }
}

// IAWSClientFactory provides the interface to create clients so that the actual implementation can be masked.
public interface IAWSClientFactory
{
    IAWSClient get(ModlAWSCredentials credentials, string service);
}