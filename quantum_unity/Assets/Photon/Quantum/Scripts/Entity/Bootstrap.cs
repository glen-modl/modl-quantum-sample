using System.Reflection;
using UnityEngine;

public class Bootstrap : MonoBehaviour
{
    public static Bootstrap Instance { get; private set; }

    public GameObject ObjectToTrack;

    [SerializeField] GameObject objectToFollow;

    private void Awake()
    {
        Instance = this;
    }
 
    private void Update()
    {
        if (ObjectToTrack != null)
        {
            objectToFollow.transform.position = new Vector3(ObjectToTrack.transform.position.x, ObjectToTrack.transform.position.y, ObjectToTrack.transform.position.z);
        }
    }

    public void PropertySet(Component member, PropertyInfo fieldInfo, object val)
    {
        Debug.Log($"Bootstrap received {member} PropertyInfo {fieldInfo} Obj Value {val}");
    }
}
