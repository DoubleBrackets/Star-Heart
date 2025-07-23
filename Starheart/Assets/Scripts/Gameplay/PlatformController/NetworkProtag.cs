using System;
using DebugTools.Logging;
using FishNet.Component.Prediction;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using SpacePhysics;
using UnityEngine;
using Utils;

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
            public float JumpVelocity;
            public Vector2 GroundCheckOffset;
            public Vector2 GroundCheckSize;
            public float GroundCheckDistance;
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

        // For debug
        private Vector2 _gravityAccel;
        private Vector2 _groundNormal;

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
            if (!Application.isPlaying)
            {
                Gizmos.color = Color.red;
                Vector3 position = _bodyAnchor.position;
                Vector2 startPos = (Vector2)position + _moveStats.GroundCheckOffset;
                Vector2 endPos = startPos - (Vector2)_bodyAnchor.up * _moveStats.GroundCheckDistance;
                Gizmos.DrawWireCube(startPos,
                    _moveStats.GroundCheckSize);
                Gizmos.DrawLine(
                    startPos,
                    endPos);
                Gizmos.DrawWireCube(endPos,
                    _moveStats.GroundCheckSize);
            }

            Gizmos.color = Color.green;
            Gizmos.DrawLine(_rb.position, _rb.position + _gravityAccel);

            Gizmos.color = Color.blue;
            Gizmos.DrawLine(_rb.position, _rb.position + _groundNormal);
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

                // Gravity calculations
                Vector2 gravityAccel = GravityManager.Instance.CalculateGravity(_rb.position);
                bool inGravity = gravityAccel != Vector2.zero;
                _gravityAccel = gravityAccel;
                Vector2 gravityUp = -gravityAccel.normalized;

                if (inGravity)
                {
                    _bodyAnchor.up = -gravityAccel.normalized;
                }

                // Ground & normal
                RaycastHit2D groundHit = UpdateGroundCheck();
                bool isGrounded = groundHit.collider != null;
                Vector2 groundNormal = isGrounded ? groundHit.normal : gravityUp;
                Vector2 groundTangent = Vector2.Perpendicular(groundNormal);

                if (state.ContainsCreated())
                {
                    // Cache the horizontal input in case we have non-created ticks
                    if (IsServerStarted || !IsOwner)
                    {
                        _horizontalInput = horizontal;
                    }

                    Debug.DrawLine(_rb.position, _rb.position + gravityUp * 0.1f, Color.green, 2f);
                }
                else
                {
                    // Use the cached input (predicting)
                    horizontal = _horizontalInput;
                    Debug.DrawLine(_rb.position, _rb.position + gravityUp * 0.1f, Color.red, 2f);
                }

                if (inGravity)
                {
                    Vector2 currentHorizontal = _rb.linearVelocity.ProjectOnPlane(groundNormal);
                    Vector2 desiredHorizontal = -horizontal * _moveStats.MoveSpeed * groundTangent;

                    Vector2 newHorizontal =
                        Vector2.MoveTowards(currentHorizontal, desiredHorizontal, _moveStats.MoveAccel * delta);

                    Vector2 moveDelta = newHorizontal - currentHorizontal;
                    Vector2 newVelocity = _rb.linearVelocity + moveDelta;

                    // Horizontal movement
                    _predictionRigidbody.Velocity(newVelocity);
                }

                if (isGrounded)
                {
                    if (data.Jump)
                    {
                        _predictionRigidbody.AddForce(gravityUp * _moveStats.JumpVelocity * _rb.mass,
                            ForceMode2D.Impulse);

                        if (state.IsTickedCreated())
                        {
                            OnJump?.Invoke();
                        }
                    }
                }

                // Gravity
                _predictionRigidbody.AddForce(gravityAccel * delta * _rb.mass,
                    ForceMode2D.Impulse);
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

        private RaycastHit2D UpdateGroundCheck()
        {
            float angle = _bodyAnchor.transform.rotation.eulerAngles.z;
            Vector2 offset = Quaternion.Euler(0, 0, angle) * _moveStats.GroundCheckOffset;
            Vector2 checkPos = _rb.position + offset;
            RaycastHit2D hit = Physics2D.BoxCast(
                checkPos,
                _moveStats.GroundCheckSize,
                angle,
                -_bodyAnchor.transform.up,
                _moveStats.GroundCheckDistance,
                _moveStats.GroundLayer);

            return hit;
        }
    }
}