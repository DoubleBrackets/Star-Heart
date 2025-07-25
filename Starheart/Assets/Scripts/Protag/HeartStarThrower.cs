using DebugTools.Logging;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace Protag
{
    public class HeartStarThrower : NetworkBehaviour
    {
        [SerializeField]
        private HeartStar _heartStarPrefab;

        [SerializeField]
        private Transform _throwPoint;

        [SerializeField]
        private NetworkProtag _networkProtag;

        [SerializeField]
        private bool _singlePlayerDebug;

        public Vector2 ThrowPoint => _throwPoint.position;
        private bool SinglePlayerDebug => _singlePlayerDebug && Application.isEditor;

        private bool _hasHeartStar;

        private void Update()
        {
            if (IsOwner && Input.GetMouseButtonDown(0))
            {
                if (_hasHeartStar)
                {
                    BadLogger.LogInfo("Throwing HeartStar", BadLogger.Actor.Client);
                    Vector3 targetPosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                    ThrowHeartStar(targetPosition);
                }
                else
                {
                    AttemptRetrieveHeartStar();
                }
            }
        }

        public override void OnStartClient()
        {
            // Only player 2 starts with the star
            _hasHeartStar = _networkProtag.PlayerNumber == 1;

            if (SinglePlayerDebug)
            {
                _hasHeartStar = true;
            }
        }

        private void OnHeartStarChanged(HeartStar prev, HeartStar next, bool asserver)
        {
            BadLogger.LogDebug(
                $"HeartStar changed from {prev} to {next} on protag {_networkProtag.PlayerNumber}",
                BadLogger.Actor.Client);
        }

        private void ThrowHeartStar(Vector2 targetPos)
        {
            if (!_hasHeartStar)
            {
                BadLogger.LogError("HeartStar is already out. Cannot throw another one.", BadLogger.Actor.Server);
                return;
            }

            Vector2 throwPos = ThrowPoint;
            HeartStar heartStar = Instantiate(_heartStarPrefab, throwPos, Quaternion.identity);
            heartStar.Initialize(targetPos, throwPos, _networkProtag.PlayerNumber);

            Spawn(heartStar.NetworkObject, LocalConnection);
            _hasHeartStar = false;
            BadLogger.LogInfo($"HeartStar thrown to {targetPos} from {throwPos}", BadLogger.Actor.Server);
        }

        public void Retrieve()
        {
            Retrieve_RPC(Owner);
        }

        [ServerRpc(RequireOwnership = false)]
        private void Retrieve_RPC(NetworkConnection owner, NetworkConnection conn = null)
        {
            Retrieve_TargetRpc(owner);
        }

        [TargetRpc]
        private void Retrieve_TargetRpc(NetworkConnection conn)
        {
            _hasHeartStar = true;
        }

        private void AttemptRetrieveHeartStar()
        {
            if (_hasHeartStar)
            {
                return;
            }

            var heartStar = FindFirstObjectByType<HeartStar>();

            if (heartStar == null)
            {
                BadLogger.LogInfo("No HeartStar to retrieve.", BadLogger.Actor.Server);
                return;
            }

            int sourceProtag = heartStar.SourceProtagNumber;
            int protagNumber = _networkProtag.PlayerNumber;
            BadLogger.LogDebug(
                $"Retrieving heartstar from p {sourceProtag} as {protagNumber}");

            if (sourceProtag == protagNumber && !SinglePlayerDebug)
            {
                return;
            }

            heartStar.Retrieve(this);

            BadLogger.LogInfo("HeartStar retrieved", BadLogger.Actor.Server);
        }
    }
}