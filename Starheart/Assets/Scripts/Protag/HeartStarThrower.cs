using DebugTools.Logging;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.Events;

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

        [Header("Indicators")]

        [SerializeField]
        private GameObject _hasHeartstarIndicator;

        [SerializeField]
        private LineRenderer _retrieveIndicator;

        [SerializeField]
        private float _length;

        [Header("Events")]

        [SerializeField]
        private UnityEvent _onThrowHeartStar;

        [SerializeField]
        private UnityEvent _onRetrieveHeartStar;

        public Vector2 ThrowPoint => _throwPoint.position;
        private bool SinglePlayerDebug => _singlePlayerDebug && Application.isEditor;

        public bool InputEnabled { get; set; } = true;
        private readonly SyncVar<bool> _hasHeartStar = new(false);

        private bool _hasHeartStarLocal;

        public void Awake()
        {
            OnHasHeartStarChanged(false, false, false);
            _hasHeartStar.OnChange += OnHasHeartStarChanged;
        }

        private void Update()
        {
            if (IsOwner && Input.GetMouseButtonDown(0) && InputEnabled)
            {
                if (_hasHeartStarLocal)
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

            // fuck it
            if (HeartStar.SpawnedHeartStar != null)
            {
                bool isRetrieveable = HeartStar.SpawnedHeartStar.SourceProtagNumber != _networkProtag.PlayerNumber ||
                                      SinglePlayerDebug;
                _retrieveIndicator.enabled = isRetrieveable;

                if (isRetrieveable)
                {
                    Vector2 dir = HeartStar.SpawnedHeartStar.VisualBodyPosition - ThrowPoint;
                    _retrieveIndicator.SetPositions(new Vector3[]
                    {
                        ThrowPoint,
                        ThrowPoint + dir.normalized * _length
                    });
                }
            }
            else
            {
                _retrieveIndicator.enabled = false;
            }
        }

        public void OnDestroy()
        {
            _hasHeartStar.OnChange -= OnHasHeartStarChanged;
        }

        private void OnHasHeartStarChanged(bool prev, bool next, bool asserver)
        {
            if (!prev && next)
            {
                BadLogger.LogInfo("HeartStar acquired", BadLogger.Actor.Client);
                _onRetrieveHeartStar?.Invoke();
            }
            else if (prev && !next)
            {
                BadLogger.LogInfo("HeartStar thrown", BadLogger.Actor.Client);
                _onThrowHeartStar?.Invoke();
            }

            _hasHeartStarLocal = next;
            _hasHeartstarIndicator.SetActive(next);
        }

        public override void OnStartClient()
        {
            if (SinglePlayerDebug || _networkProtag.PlayerNumber == 1)
            {
                SetHasStarHeart_RPC(true);
            }

            _retrieveIndicator.positionCount = 2;
        }

        private void ThrowHeartStar(Vector2 targetPos)
        {
            if (!_hasHeartStarLocal)
            {
                BadLogger.LogError("HeartStar is already out. Cannot throw another one.", BadLogger.Actor.Server);
                return;
            }

            _hasHeartStarLocal = false;
            SetHasStarHeart_RPC(false);

            Vector2 throwPos = ThrowPoint;
            float angle = Vector2.SignedAngle(Vector2.up, targetPos - throwPos);
            HeartStar heartStar = Instantiate(_heartStarPrefab, throwPos, Quaternion.Euler(0, 0, angle));
            heartStar.Initialize(targetPos, throwPos, _networkProtag.PlayerNumber);

            Spawn(heartStar.NetworkObject, LocalConnection);
            BadLogger.LogInfo($"HeartStar thrown to {targetPos} from {throwPos}", BadLogger.Actor.Server);
        }

        public void Retrieve()
        {
            SetHasStarHeart_RPC(true);
        }

        [ServerRpc(RequireOwnership = false)]
        private void SetHasStarHeart_RPC(bool val)
        {
            _hasHeartStar.Value = val;
        }

        private void AttemptRetrieveHeartStar()
        {
            if (_hasHeartStarLocal)
            {
                return;
            }

            var heartStar = HeartStar.SpawnedHeartStar;

            if (heartStar == null)
            {
                BadLogger.LogInfo("No HeartStar to retrieve.", BadLogger.Actor.Server);
                return;
            }

            int sourceProtag = heartStar.SourceProtagNumber;
            int protagNumber = _networkProtag.PlayerNumber;
            BadLogger.LogDebug(
                $"Retrieving heartstar from p {sourceProtag} as {protagNumber}");

            if (sourceProtag != protagNumber || SinglePlayerDebug)
            {
                heartStar.Retrieve(this);

                BadLogger.LogInfo("HeartStar began retrieving", BadLogger.Actor.Server);
            }
        }
    }
}