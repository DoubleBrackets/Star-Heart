using System;
using DebugTools.Logging;
using FishNet.Component.Prediction;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using UnityEngine;

namespace PlatformController
{
    /// <summary>
    ///     Networked 2D rigidbody character controller with prediction
    /// </summary>
    public class NetworkProtag : NetworkBehaviour
    {
        [Serializable]
        public struct MoveStats
        {
            public float MoveSpeed;
            public float MoveAccel;
            public float JumpHeight;
            public float Gravity;
            public Vector2 GroundCheckOffset;
            public Vector2 GroundCheckSize;
            public LayerMask GroundLayer;
        }

        private struct MovementData : IReplicateData
        {
            public float HorizontalInput;
            public bool Jump;
            public float Salty;

            public MovementData(float horizontalInput, bool jump, float salty)
            {
                HorizontalInput = horizontalInput;
                Jump = jump;
                Salty = salty;
                _tick = 0;
            }

            private uint _tick;

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
                // Internal to Fishnet
            }
        }

        private struct ReconcileData : IReconcileData
        {
            public readonly PredictionRigidbody2D Rigidbody2DState;
            public float HorizontalInput;

            private uint _tick;

            public ReconcileData(PredictionRigidbody2D rigidbody2DState, float horizontalInput)
            {
                Rigidbody2DState = rigidbody2DState;
                HorizontalInput = horizontalInput;
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

        [Header("Depends")]

        [SerializeField]
        private Rigidbody2D _rb;

        [SerializeField]
        private Transform _bodyAnchor;

        [SerializeField]
        private Animator _animator;

        [SerializeField]
        private SpriteRenderer _spriteRenderer;

        [Header("Config")]

        [SerializeField]
        private MoveStats _moveStats;

        [SerializeField]
        private bool _collideWithPlayers;

        [SerializeField]
        private int _spectatorTicksToPredict;

        public event Action OnJump;

        private float _horizontalInput;
        private bool _jumpInput;

        private PredictionRigidbody2D _predictionRigidbody;

        private Rigidbody2DState _rbState;
        private bool _frozen;

        private int _remainingTicksToPredict;

        private void Awake()
        {
            _predictionRigidbody = new PredictionRigidbody2D();
            _predictionRigidbody.Initialize(_rb);
            Debug.Log($"Initialized PredictionRigidbody for {name}");

            if (!_collideWithPlayers)
            {
                Physics2D.IgnoreLayerCollision(LayerMask.NameToLayer("Protag"), LayerMask.NameToLayer("Protag"));
            }
        }

        private void Update()
        {
            if (IsOwner)
            {
                // Read inputs
                _horizontalInput = Input.GetAxisRaw("Horizontal");
                _jumpInput = _jumpInput || Input.GetButtonDown("Jump");
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube((Vector2)_bodyAnchor.position + _moveStats.GroundCheckOffset,
                _moveStats.GroundCheckSize);
        }

        public override void OnStartNetwork()
        {
            TimeManager.OnTick += TimeManager_OnTick;
            TimeManager.OnPostTick += TimeManager_PostTick;
            RenameGameObject();
        }

        private void RenameGameObject()
        {
            gameObject.name = "NetworkProtag";
            if (Owner.IsHost)
            {
                gameObject.name += "[Host]";
            }
            else
            {
                gameObject.name += "[Client]";
            }

            gameObject.name += $"[Owner={OwnerId}]";
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
                var data = new MovementData(_horizontalInput, _jumpInput, 1);
                Replicate(data);
            }
            else
            {
                Replicate(default);
            }
        }

        private void TimeManager_PostTick()
        {
            _jumpInput = false;
            CreateReconcile();
        }

        [Replicate]
        private void Replicate(
            MovementData data,
            ReplicateState state = ReplicateState.Invalid,
            Channel channel = Channel.Unreliable)
        {
            BadLogger.LogTrace(
                $"Replicating {state.ContainsTicked()} {state.ContainsReplayed()} {state.ContainsCreated()} {data.HorizontalInput} {data.Jump} tick {data.GetTick()} {name}");
            var delta = (float)TimeManager.TickDelta;

            float horizontal = data.HorizontalInput;

            bool canPause = !IsOwner && !IsServerStarted;

            if (state.IsFuture() && _remainingTicksToPredict <= 0)
            {
                // Pause for future ticks to prevent snapping from predicting on non-owning clients
                if (canPause)
                {
                    NetworkObject.RigidbodyPauser.Pause();
                }
            }
            else
            {
                // Predict a configurable number of ticks into the future by assuming held inputs
                if (state.IsFuture())
                {
                    _remainingTicksToPredict--;
                }

                // Unpause for created ticks
                if (canPause)
                {
                    NetworkObject.RigidbodyPauser.Unpause();
                }

                if (state.ContainsCreated())
                {
                    // Cache the horizontal input in case we have non-created ticks
                    if (IsServerStarted || !IsOwner)
                    {
                        _horizontalInput = horizontal;
                    }

                    Debug.DrawLine(_rb.position, _rb.position + Vector2.up * 0.1f, Color.green, 2f);
                }
                else
                {
                    // Use the cached input (predicting)
                    horizontal = _horizontalInput;
                    Debug.DrawLine(_rb.position, _rb.position + Vector2.up * 0.1f, Color.red, 2f);
                }

                float currentHorizontal = _rb.linearVelocity.x;
                float desiredHorizontal = horizontal * _moveStats.MoveSpeed;

                float newHorizontal =
                    Mathf.MoveTowards(currentHorizontal, desiredHorizontal, _moveStats.MoveAccel * delta);

                // Horizontal movement
                _predictionRigidbody.Velocity(new Vector2(newHorizontal, _rb.linearVelocity.y));

                // Jump movement
                bool isGrounded = UpdateGroundCheck();
                if (isGrounded)
                {
                    if (data.Jump)
                    {
                        float jumpVel = Mathf.Sqrt(2 * -_moveStats.Gravity * _moveStats.JumpHeight);
                        _predictionRigidbody.AddForce(Vector2.up * jumpVel * _rb.mass, ForceMode2D.Impulse);

                        if (state.IsTickedCreated())
                        {
                            OnJump?.Invoke();
                        }
                    }
                }
                else
                {
                    // Gravity
                    _predictionRigidbody.AddForce(Vector2.up * (_moveStats.Gravity * delta) * _rb.mass,
                        ForceMode2D.Impulse);
                }

                _predictionRigidbody.Simulate();

                if (state.ContainsCreated())
                {
                    ReplicateVisuals((int)horizontal, isGrounded);
                }
            }
        }

        private void ReplicateVisuals(int horizontalInput, bool isGrounded)
        {
            if (horizontalInput > 0)
            {
                _spriteRenderer.flipX = false;
            }
            else if (horizontalInput < 0)
            {
                _spriteRenderer.flipX = true;
            }

            _animator.SetFloat("Speed", _moveStats.MoveSpeed * Mathf.Abs(horizontalInput));
            _animator.SetBool("Air", !isGrounded);
        }

        public override void CreateReconcile()
        {
            if (!IsServerStarted)
            {
                return;
            }

            var data = new ReconcileData(_predictionRigidbody, _horizontalInput);
            Reconcile(data);
        }

        [Reconcile]
        private void Reconcile(ReconcileData data, Channel channel = Channel.Unreliable)
        {
            BadLogger.LogTrace(
                $"Reconciled tick {data.GetTick()} {name}");
            _remainingTicksToPredict = _spectatorTicksToPredict;
            _predictionRigidbody.Reconcile(data.Rigidbody2DState);
            ReplicateVisuals((int)data.HorizontalInput, true);
        }

        private bool UpdateGroundCheck()
        {
            Vector2 checkPos = _rb.position + _moveStats.GroundCheckOffset;
            Collider2D hit = Physics2D.OverlapBox(checkPos, _moveStats.GroundCheckSize, 0, _moveStats.GroundLayer);

            return hit != null;
        }
    }
}