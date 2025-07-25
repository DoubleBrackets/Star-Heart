using System.Collections.Generic;
using DebugTools.Logging;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Serializing;
using FishNet.Transporting;
using SpacePhysics;
using UnityEngine;
using UnityEngine.Events;

namespace Protag
{
    public class HeartStar : NetworkBehaviour
    {
        public enum HeartStarState
        {
            Launching,
            Idling,
            Retrieving
        }

        private struct ReplicateData : IReplicateData
        {
            public float Salty;
            public Vector2 TargetPosition;
            public HeartStarThrower AttemptedRetriever;

            private uint _tick;

            public ReplicateData(Vector2 targetPosition, HeartStarThrower attemptedRetriever)
            {
                TargetPosition = targetPosition;
                AttemptedRetriever = attemptedRetriever;
                Salty = 1f;
                _tick = 0;
            }

            public uint GetTick()
            {
                return _tick;
            }

            public void SetTick(uint value)
            {
                _tick = value;
            }

            public void Dispose()
            {
            }
        }

        private struct ReconcileData : IReconcileData
        {
            public Vector2 VisualBodyPosition;
            public Vector2 GravityPosition;

            public Vector2 TargetPosition;
            public HeartStarState State;

            public bool GravityEnabled;
            public float GravEnableTimer;

            public float GravDisableTimer;

            public HeartStarThrower Retriever;

            private uint _tick;

            public ReconcileData(Vector2 visualBodyPosition,
                Vector2 gravityPosition,
                Vector2 targetPosition,
                HeartStarState state,
                bool gravityEnabled,
                float gravEnableTimer,
                float gravDisableTimer,
                HeartStarThrower retriever)
            {
                VisualBodyPosition = visualBodyPosition;
                GravityPosition = gravityPosition;
                TargetPosition = targetPosition;
                State = state;
                GravEnableTimer = gravEnableTimer;
                GravityEnabled = gravityEnabled;
                GravDisableTimer = gravDisableTimer;
                Retriever = retriever;
                _tick = 0;
            }

            public uint GetTick()
            {
                return _tick;
            }

            public void SetTick(uint value)
            {
                _tick = value;
            }

            public void Dispose()
            {
            }
        }

        [SerializeField]
        private float _travelLerpExp;

        [SerializeField]
        private float _retrieveSpeed;

        [SerializeField]
        private float _gravEnableDelay;

        [SerializeField]
        private float _gravDisableDelay;

        [SerializeField]
        private GravitySource _gravitySource;

        [SerializeField]
        private Transform _gravityBody;

        [SerializeField]
        private Transform _visualBody;

        [SerializeField]
        private SpriteRenderer _heartSpriteRenderer;

        [SerializeField]
        private List<Color> _heartColors;

        public UnityEvent OnEnterIdle;
        public UnityEvent OnEnterRetrieve;

        public int SourceProtagNumber => _sourceProtagNumber;

        private HeartStarState _state;
        private float _gravEnabledTimer;
        private float _gravDisableTimer;
        private bool _gravityEnabled;
        private Vector2 _targetPosition;

        private HeartStarThrower _heartRetriever;
        private HeartStarThrower _attemptedRetriever;

        /// <summary>
        ///     The protag that threw this star (0 or 1)
        /// </summary>
        private int _sourceProtagNumber;

        private bool _isInitialized;

        private void OnDrawGizmos()
        {
            Gizmos.color = _sourceProtagNumber == 0 ? Color.red : Color.blue;
            Gizmos.DrawWireSphere(_visualBody.position, 0.5f);
        }

        public override void WritePayload(NetworkConnection connection, Writer writer)
        {
            bool isPredictedSpawner = NetworkObject.PredictedSpawner.IsLocalClient;

            /* It is worth noting that the server will still call WritePayload when
             * spawning the object for clients, even if a predicted spawn.
             * Should a client send a predicted spawn the server will receive ReadPayload
             * for the predicted spawn, and the server will also be given an opportunity to
             * send a payload to other clients which are not the predicted spawner. If you wish to
             * forward a payload from a predicted spawner cache the values locally, and
             */

            writer.WriteVector2(_targetPosition);
            writer.WriteVector2(_visualBody.position);
            writer.WriteInt32(_sourceProtagNumber);
        }

        public override void ReadPayload(NetworkConnection connection, Reader reader)
        {
            /* You can also check if the payload is coming from the predicted spawner as well.
             *
             * ReadPayload would be first called on the server, and if the connection IsValid, we know
             * that the connection is a predicted spawner, as that is the only time connection would be
             * valid on the server. */
            bool isFromPredictedSpawner = IsServerStarted && connection.IsValid;

            // Predicted spawn owner already called initialize, so don't double initialize
            Vector2 targetPosition = reader.ReadVector2();
            Vector2 throwPos = reader.ReadVector2();
            int sourceProtag = reader.ReadInt32();
            Initialize(targetPosition, throwPos, sourceProtag);
        }

        public override void OnStartClient()
        {
            _gravitySource.IsActive = false;
        }

        public void Initialize(Vector2 targetPosition, Vector2 throwPos, int sourceProtag)
        {
            if (_isInitialized)
            {
                return;
            }

            _isInitialized = true;
            BadLogger.LogDebug("Initializing HeartStar", BadLogger.Actor.Client);
            _visualBody.position = throwPos;
            _targetPosition = targetPosition;
            _state = HeartStarState.Launching;
            _gravityEnabled = false;
            _gravEnabledTimer = 0f;
            _gravDisableTimer = 0f;
            _heartRetriever = null;
            _sourceProtagNumber = sourceProtag;
            _gravityBody.position = targetPosition;

            _heartSpriteRenderer.color = _heartColors[_sourceProtagNumber];

            float angle = Vector2.SignedAngle(Vector2.up, _targetPosition - throwPos);
            _visualBody.rotation = Quaternion.Euler(0, 0, angle);
        }

        public override void OnStartNetwork()
        {
            TimeManager.OnTick += TimeManager_OnTick;
            TimeManager.OnPostTick += TimeManager_PostTick;
        }

        public override void OnStopNetwork()
        {
            TimeManager.OnTick -= TimeManager_OnTick;
            TimeManager.OnPostTick -= TimeManager_PostTick;
        }

        private void TimeManager_OnTick()
        {
            if (IsController)
            {
                var data = new ReplicateData(_targetPosition, _attemptedRetriever);
                Replicate(data);
                _attemptedRetriever = null;
            }
            else
            {
                Replicate(default);
            }
        }

        private void TimeManager_PostTick()
        {
            CreateReconcile();
        }

        [Replicate]
        private void Replicate(
            ReplicateData data,
            ReplicateState state = ReplicateState.Invalid,
            Channel channel = Channel.Unreliable)
        {
            BadLogger.LogTrace(
                $"Replicating {data.GetTick()} {state.ContainsTicked()} {state.ContainsReplayed()} {state.ContainsCreated()} " +
                $"{_visualBody.position} {_gravEnabledTimer} {_gravityEnabled} {_state} " +
                $"{name}");

            var delta = (float)TimeManager.TickDelta;
            Vector2 currentPos = _visualBody.position;

            HeartStarState currentState = _state;

            if (currentState == HeartStarState.Launching)
            {
                Vector2 desiredPosition = _targetPosition;

                if (state.ContainsCreated())
                {
                    desiredPosition = data.TargetPosition;
                }

                float dist = Vector2.Distance(currentPos, desiredPosition);

                if (state.ContainsCreated())
                {
                    if (dist < 0.2f)
                    {
                        // Close enough, stop moving
                        _state = HeartStarState.Idling;

                        if (state.ContainsTicked() && !state.ContainsReplayed())
                        {
                            OnEnterIdle?.Invoke();
                        }
                    }
                    else
                    {
                        float t = 1 - Mathf.Pow(0.01f, delta * _travelLerpExp);
                        Vector2 newPosition = Vector2.Lerp(currentPos, desiredPosition, t);
                        _visualBody.position = newPosition;
                    }
                }
            }
            else if (currentState == HeartStarState.Idling)
            {
                if (data.AttemptedRetriever != null)
                {
                    _state = HeartStarState.Retrieving;
                    _heartRetriever = data.AttemptedRetriever;
                    _gravDisableTimer = 0f;

                    if (state.ContainsTicked() && !state.ContainsReplayed())
                    {
                        OnEnterRetrieve?.Invoke();
                    }
                }
            }
            else if (currentState == HeartStarState.Retrieving)
            {
                if (_gravDisableTimer < _gravDisableDelay)
                {
                    _gravDisableTimer += delta;
                    if (_gravDisableTimer >= _gravDisableDelay)
                    {
                        _gravityEnabled = false;
                    }
                }

                if (state.ContainsCreated())
                {
                    Vector2 desiredPosition = _heartRetriever.ThrowPoint;

                    float dist = Vector2.Distance(currentPos, desiredPosition);

                    if (dist < 0.4f)
                    {
                        // Close enough, remove object
                        _heartRetriever.Retrieve();
                        Despawn(NetworkObject);
                    }
                    else
                    {
                        float t = 1 - Mathf.Pow(0.01f, delta * _travelLerpExp);
                        Vector2 newPosition = Vector2.Lerp(currentPos, desiredPosition, t);
                        newPosition = Vector2.MoveTowards(newPosition, desiredPosition, _retrieveSpeed * delta);
                        _visualBody.position = newPosition;

                        float angle = Vector2.SignedAngle(Vector2.up, desiredPosition - currentPos);
                        _visualBody.rotation = Quaternion.Euler(0, 0, angle);
                    }
                }
            }

            if (_gravEnabledTimer < _gravEnableDelay && state.ContainsCreated())
            {
                _gravEnabledTimer += delta;
                if (_gravEnabledTimer >= _gravEnableDelay)
                {
                    _gravityEnabled = true;
                }
            }

            _gravitySource.IsActive = _gravityEnabled;
        }

        public override void CreateReconcile()
        {
            if (!IsServerStarted)
            {
                return;
            }

            var data = new ReconcileData(
                _visualBody.position,
                _gravityBody.position,
                _targetPosition,
                _state,
                _gravityEnabled,
                _gravEnabledTimer,
                _gravDisableTimer,
                _heartRetriever);

            Reconcile(data);
        }

        [Reconcile]
        private void Reconcile(ReconcileData data, Channel channel = Channel.Unreliable)
        {
            BadLogger.LogTrace(
                $"Reconciled tick {data.GetTick()} {data.GravEnableTimer} {data.VisualBodyPosition} {name}");
            _state = data.State;
            _targetPosition = data.TargetPosition;
            _visualBody.position = data.VisualBodyPosition;
            _gravityBody.position = data.GravityPosition;
            _gravityEnabled = data.GravityEnabled;
            _gravEnabledTimer = data.GravEnableTimer;
            _gravDisableTimer = data.GravDisableTimer;
            _heartRetriever = data.Retriever;
        }

        public void Retrieve(HeartStarThrower retrieveThrower)
        {
            Retrieve_RPC(retrieveThrower);
        }

        [ServerRpc(RequireOwnership = false)]
        private void Retrieve_RPC(HeartStarThrower retrieveThrower)
        {
            _attemptedRetriever = retrieveThrower;
            TargetRetrieve_RPC(Owner, retrieveThrower);
        }

        [TargetRpc]
        private void TargetRetrieve_RPC(NetworkConnection conn, HeartStarThrower retrieveThrower)
        {
            _attemptedRetriever = retrieveThrower;
        }
    }
}