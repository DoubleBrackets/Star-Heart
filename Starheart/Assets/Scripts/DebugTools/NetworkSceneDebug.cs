using System.Collections.Generic;
using DebugTools.Logging;
using FishNet;
using FishNet.Connection;
using FishNet.Managing.Client;
using FishNet.Managing.Server;
using FishNet.Managing.Transporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DebugTools
{
    public class NetworkSceneDebug : MonoBehaviour
    {
        [SerializeField]
        private int _fontSize;

        [SerializeField]
        private Color _color;

        private GUIStyle _style;

        private void Awake()
        {
            _style = new GUIStyle();
            _style.fontSize = _fontSize;
            _style.normal.textColor = _color;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.L))
            {
                TransportManager transportManager = InstanceFinder.TransportManager;
                if (transportManager != null)
                {
                    bool isEnabled = transportManager.LatencySimulator.GetEnabled();
                    transportManager.LatencySimulator.SetEnabled(!isEnabled);
                    BadLogger.LogInfo($"Latency simulation toggled to {!isEnabled}");
                }
                else
                {
                    BadLogger.LogWarning("TransportManager not found. Cannot toggle latency simulation.");
                }
            }
        }

        private void OnGUI()
        {
            ServerManager serverManager = InstanceFinder.ServerManager;
            if (serverManager)
            {
                Dictionary<Scene, HashSet<NetworkConnection>> sceneConnections =
                    InstanceFinder.SceneManager.SceneConnections;

                foreach (KeyValuePair<Scene, HashSet<NetworkConnection>> pair in sceneConnections)
                {
                    Scene scene = pair.Key;
                    HashSet<NetworkConnection> conns = pair.Value;

                    GUILayout.Label($"Scene: {scene.name}", _style);

                    foreach (NetworkConnection conn in conns)
                    {
                        GUILayout.Label($"Connection: {conn}", _style);
                    }
                }
            }

            ClientManager clientManager = InstanceFinder.ClientManager;
            if (clientManager.Started)
            {
                GUILayout.Label($"Local Client: {clientManager.Connection.ClientId}", _style);
                GUILayout.Label($"Tick: {InstanceFinder.TimeManager.Tick}", _style);
                GUILayout.Label($"LocalTick: {InstanceFinder.TimeManager.LocalTick}", _style);
            }

            TransportManager transportManager = InstanceFinder.TransportManager;
            if (transportManager)
            {
                GUILayout.Label($"Simulating Latency: {transportManager.LatencySimulator.GetEnabled()}", _style);
            }
        }
    }
}