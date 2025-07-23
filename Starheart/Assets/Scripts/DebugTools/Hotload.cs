using FishNet;
using GameKit.Dependencies.Utilities.Types;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DebugTools
{
    public class Hotload : MonoBehaviour
    {
        [SerializeField]
        [Scene]
        private string _mainMenu;

        private void Start()
        {
            if (InstanceFinder.ClientManager == null)
            {
                SceneManager.LoadScene(_mainMenu);
            }
        }
    }
}