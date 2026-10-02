using UnityEngine;


public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;

    public static bool TryGetExistingInstance(out T instance)
    {
        if (_instance == null)
        {
            _instance = FindFirstObjectByType<T>();
        }

        instance = _instance;
        return instance != null;
    }

    public static T Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<T>();
                if (_instance == null)
                {
                    SetUpInstance();
                }
            }

            return _instance;
        }
    }

    //  가상함수
    public virtual void Awake()
    {
        RemoveDuplicates();
    }

    private static void SetUpInstance()
    {
        _instance = FindFirstObjectByType<T>();

        if (_instance == null)
        {
            GameObject obj = new GameObject();
            obj.name = typeof(T).Name;
            _instance = obj.AddComponent<T>();
            DontDestroyOnLoad(obj);
        }
    }

    private void RemoveDuplicates()
    {
        if (_instance == null)
        {
            _instance = this as T;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
