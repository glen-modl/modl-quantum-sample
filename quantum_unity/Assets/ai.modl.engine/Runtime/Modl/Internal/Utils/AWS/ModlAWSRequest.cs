using UnityEngine.Networking;

// The ModlAWSRequest implements the IAWSRequest interface to
// - send HTTP requests to AWS;
// - notify back its user of the completed request.
public class ModlAWSRequest : IAWSRequest
{

    public readonly UnityWebRequest webRequest;
    
    public ModlAWSRequest(UnityWebRequest request)
    {
        webRequest = request;
    }

    public override void SendRequest()
    {
        var asyncOperation = webRequest.SendWebRequest();
        asyncOperation.completed += operation => TriggerCompleted();
    }
}
