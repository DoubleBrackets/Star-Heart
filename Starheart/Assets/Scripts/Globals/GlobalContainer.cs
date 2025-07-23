using UnityEngine;

namespace Globals
{
    public class GlobalContainer : MonoBehaviour
    {
        private static GlobalContainer _instance;

        [SerializeField]
        private GameObject _globalPrefab;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);

                // Instantiate the global prefab if it exists
                if (_globalPrefab != null)
                {
                    Instantiate(_globalPrefab, transform);
                }
            }
            else
            {
                Destroy(gameObject); // Ensure only one instance exists
            }
        }
    }
}