using DebugTools.Logging;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using UnityEngine;

namespace Protag
{
    public class ProtagManager : NetworkBehaviour
    {
        public struct ProtagData
        {
            public int PlayerNumber;
        }

        [SerializeField]
        private NetworkObject _networkPrefab;

        [SerializeField]
        private Transform _spawnPos;

        public static ProtagManager Instance { get; private set; }

        private readonly SyncDictionary<NetworkConnection, ProtagData> protags = new();

        [ServerRpc(RequireOwnership = false)]
        private void SpawnPlayer(NetworkConnection owner, Vector3 pos, NetworkConnection connection = null)
        {
            NetworkObject protag = Instantiate(_networkPrefab);
            protag.transform.position = pos;
            Spawn(protag, owner);
        }

        public override void OnStartServer()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }

            Instance.ServerManager.OnRemoteConnectionState += HandleRemoteConnectionState;
        }

        public override void OnStopServer()
        {
            Instance.ServerManager.OnRemoteConnectionState -= HandleRemoteConnectionState;
        }

        public override void OnStartClient()
        {
            if (IsServerInitialized)
            {
                // Register host client
                RegisterPlayer(LocalConnection);
            }
            else
            {
                SpawnPlayer(LocalConnection, _spawnPos.position);
            }
        }

        private void HandleRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs stateArgs)
        {
            if (stateArgs.ConnectionState == RemoteConnectionState.Started)
            {
                RegisterPlayer(conn);
            }
            else if (stateArgs.ConnectionState == RemoteConnectionState.Stopped)
            {
                UnregisterPlayer(conn);
            }
        }

        [Server]
        private void RegisterPlayer(NetworkConnection conn)
        {
            if (!protags.ContainsKey(conn))
            {
                int playerCount = protags.Count;

                BadLogger.LogInfo(
                    $"ProtagManager: Adding new protag for connection {conn.ClientId} with player number {playerCount}",
                    BadLogger.Actor.Server);

                var data = new ProtagData
                {
                    PlayerNumber = playerCount
                };
                protags[conn] = data;
            }
        }

        [Server]
        private void UnregisterPlayer(NetworkConnection conn)
        {
            if (protags.ContainsKey(conn))
            {
                BadLogger.LogInfo(
                    $"ProtagManager: Removing protag for connection {conn.ClientId} with player number {protags[conn].PlayerNumber}",
                    BadLogger.Actor.Server);

                protags.Remove(conn);
            }
        }

        public bool GetProtagData(NetworkConnection connection, out ProtagData data)
        {
            if (protags.TryGetValue(connection, out data))
            {
                return true;
            }

            data = default;
            return false;
        }
    }
}