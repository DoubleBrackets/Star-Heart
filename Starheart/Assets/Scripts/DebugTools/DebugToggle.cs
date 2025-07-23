using UnityEngine;

namespace DebugTools
{
    public class DebugToggle : MonoBehaviour
    {
        [SerializeField]
        private GameObject _debugObject;

        private void Awake()
        {
            _debugObject.SetActive(Application.isEditor);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.P))
            {
                _debugObject.SetActive(!_debugObject.activeSelf);
            }
        }
    }
}