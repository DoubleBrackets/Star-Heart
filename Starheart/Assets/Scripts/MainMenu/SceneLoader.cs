using System.Collections.Generic;
using System.Linq;
using DebugTools.Logging;
using FishNet;
using FishNet.Managing.Scened;
using FishNet.Managing.Server;
using FishNet.Object;
using FishNet.Transporting;
using GameKit.Dependencies.Utilities.Types;
using UnityEngine;
using SceneManager = UnityEngine.SceneManagement.SceneManager;

namespace MainMenu
{
    public class SceneLoader : MonoBehaviour
    {
        [Scene]
        [SerializeField]
        private string _mainMenuScene;

        [Scene]
        [SerializeField]
        private string _gameplayScene;

        private void Start()
        {
            InstanceFinder.ServerManager.OnServerConnectionState += OnServerConnectionState;
            InstanceFinder.ClientManager.OnClientConnectionState += OnClientConnectionState;
        }

        private void OnDestroy()
        {
            InstanceFinder.ServerManager.OnServerConnectionState -= OnServerConnectionState;
            InstanceFinder.ClientManager.OnClientConnectionState -= OnClientConnectionState;
        }

        private void OnServerConnectionState(ServerConnectionStateArgs state)
        {
            BadLogger.LogDebug($"MinigameManager: Server connection state: {state.ConnectionState}",
                BadLogger.Actor.Server);
            if (state.ConnectionState == LocalConnectionState.Stopped)
            {
                SceneManager.LoadScene(_mainMenuScene);
            }
            else if (state.ConnectionState == LocalConnectionState.Started)
            {
                InstanceFinder.SceneManager.OnLoadEnd += UnloadMainMenuOnGameplay;
                LoadGameplayScene();
            }
        }

        private void OnClientConnectionState(ClientConnectionStateArgs state)
        {
            if (state.ConnectionState == LocalConnectionState.Stopped && !InstanceFinder.ServerManager.Started)
            {
                SceneManager.LoadScene(_mainMenuScene);
            }

            if (state.ConnectionState == LocalConnectionState.Started)
            {
                InstanceFinder.SceneManager.OnLoadEnd += UnloadMainMenuOnGameplay;
            }
        }

        private void UnloadMainMenuOnGameplay(SceneLoadEndEventArgs args)
        {
            InstanceFinder.SceneManager.OnLoadEnd -= UnloadMainMenuOnGameplay;
            SceneManager.UnloadSceneAsync(_mainMenuScene);
        }

        private void OnMinigameLoaded(SceneLoadEndEventArgs arg)
        {
            InstanceFinder.SceneManager.OnLoadEnd -= OnMinigameLoaded;
            BadLogger.LogDebug("Minigame loaded!", BadLogger.Actor.Server);
        }

        private void UnloadGameplay()
        {
            DespawnAllSceneObjects(_gameplayScene);

            var sceneUnloadData = new SceneUnloadData(_gameplayScene);

            BadLogger.LogInfo($"Unloading {_gameplayScene}");

            InstanceFinder.SceneManager.UnloadGlobalScenes(sceneUnloadData);
        }

        private void DespawnAllSceneObjects(string scene)
        {
            BadLogger.LogInfo($"Despawning all objects in {scene}");

            GameObject[] gameObjects = SceneManager.GetActiveScene().GetRootGameObjects();

            List<NetworkObject> nobs = gameObjects.SelectMany(a => a.GetComponentsInChildren<NetworkObject>()).ToList();

            foreach (NetworkObject no in nobs)
            {
                InstanceFinder.ServerManager.Despawn(no);
            }
        }

        /// <summary>
        ///     Loads the gameplay scene as a global scene
        /// </summary>
        [Server]
        private void LoadGameplayScene()
        {
            ServerManager serverManager = InstanceFinder.ServerManager;
            if (!serverManager.Started)
            {
                return;
            }

            var sceneLoadData = new SceneLoadData(_gameplayScene);
            sceneLoadData.Options.AutomaticallyUnload = true;
            sceneLoadData.PreferredActiveScene = new PreferredScene(new SceneLookupData(_gameplayScene));

            InstanceFinder.SceneManager.LoadGlobalScenes(sceneLoadData);
        }
    }
}