using System;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using Gameplay;
using SpacePhysics;
using UnityEngine;
using UnityEngine.Events;
using Utils;

namespace Protag
{
    /// <summary>
    ///     Networked 2D rigidbody character controller with prediction
    /// </summary>
    public class ProtagController : NetworkBehaviour
    {
        public enum ProtagControllerState
        {
            InPlanet,
            InSpace,
            InDialog
        }

        [Serializable]
        public struct MoveStats
        {
            public float MoveSpeed;

            public float AlignLerpExp;
            public float AlignAngleVelocity;

            public float MaxPossibleSpeed;

            public float GroundAccel;
            public float AirMoveAccel;
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
            public GravityManager.GravityEffect GravityEffect;
            public float Salty;
            public bool InDialog;

            private uint _tick;

            public MovementData(
                float horizontalInput,
                bool jump,
                GravityManager.GravityEffect effect,
                bool inDialog)
            {
                HorizontalInput = horizontalInput;
                Jump = jump;
                GravityEffect = effect;
                Salty = 1;
                InDialog = inDialog;
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
                // Internal to Fishnet
            }
        }

        private struct ReconcileData : IReconcileData
        {
            public float HorizontalInput;
            public ProtagControllerState State;
            public readonly PredictionRigidbody2D Rigidbody2DState;

            private uint _tick;

            public ReconcileData(PredictionRigidbody2D rigidbody2DState, ProtagControllerState state,
                float horizontalInput)
            {
                Rigidbody2DState = rigidbody2DState;
                HorizontalInput = horizontalInput;
                State = state;
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

        private static readonly int SpeedAnimHash = Animator.StringToHash("Speed");
        private static readonly int AirAnimHash = Animator.StringToHash("Air");

        [Header("Depends")]

        [SerializeField]
        private Rigidbody2D _rb;

        [SerializeField]
        private Transform _bodyAnchor;

        [SerializeField]
        private Animator _animator;

        [SerializeField]
        private SpriteRenderer _spriteRenderer;

        [SerializeField]
        private ProtagCamera _protagCamera;

        [Header("Config")]

        [SerializeField]
        private MoveStats _moveStats;

        [SerializeField]
        private bool _collideWithPlayers;

        [Header("State Transitions")]

        [SerializeField]
        private UnityEvent _onEnterSpace;

        [SerializeField]
        private UnityEvent _onExitSpace;

        public bool InDialog { get; set; }

        public event Action OnJump;

        // Inputs
        private float _horizontalInput;
        private bool _jumpInput;

        private PredictionRigidbody2D _predictionRigidbody;

        private bool _frozen;

        private ProtagControllerState _state = ProtagControllerState.InPlanet;

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
                // Gravity well is an "input", since it's state is independent of protag's replicate
                // The heartstar doesn't always reconcile/replicate, so calculating this inside replicate causes issues
                // Calculating it out here works fine as long as the heartstar gravity physics never needs to correct (which it shouldn't)
                GravityManager.GravityEffect gravityEffect = GravityManager.Instance.CalculateGravity(_rb.position);
                var data = new MovementData(_horizontalInput, _jumpInput, gravityEffect, InDialog);
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
            /*BadLogger.LogTrace(
                $"B {data.GetTick()} {state.ContainsTicked()} {state.ContainsReplayed()} {state.ContainsCreated()} " +
                $"{data.GravityEffect.TotalAcceleration} {_rb.position} {_rb.linearVelocity} {data.Jump} {name}");*/
            var delta = (float)TimeManager.TickDelta;

            bool canPause = !IsOwner && !IsServerStarted;

            if (state.IsFuture())
            {
                if (canPause)
                {
                    NetworkObject.RigidbodyPauser.Pause();
                }

                return;
            }

            // Cache the horizontal input in case we have non-created ticks
            float horizontal = data.HorizontalInput;
            if (state.ContainsCreated())
            {
                if (IsServerStarted || !IsOwner)
                {
                    _horizontalInput = horizontal;
                }
            }
            else
            {
                horizontal = _horizontalInput;
            }

            // Unpause for created ticks
            if (canPause)
            {
                NetworkObject.RigidbodyPauser.Unpause();
            }

            // Gravity calculations
            GravityManager.GravityEffect gravityEffect = data.GravityEffect;
            Vector2 totalGravAccel = gravityEffect.TotalAcceleration;
            Vector2 planetGravAccel = gravityEffect.PlanetAcceleration;
            Vector2 planetGravityUp = -planetGravAccel.normalized;

            _gravityAccel = totalGravAccel;

            // Ground & normal
            RaycastHit2D groundHit = UpdateGroundCheck();
            bool isGrounded = groundHit.collider != null;
            Vector2 groundNormal = isGrounded ? groundHit.normal : planetGravityUp;

            if (_state == ProtagControllerState.InPlanet)
            {
                AlignToUpVector(planetGravityUp);

                HorizontalMovement(horizontal, groundNormal);

                // In planet jumping
                if (isGrounded && data.Jump)
                {
                    _predictionRigidbody.AddForce(planetGravityUp * _moveStats.JumpVelocity * _rb.mass,
                        ForceMode2D.Impulse);

                    if (state.IsTickedCreated())
                    {
                        OnJump?.Invoke();
                    }
                }

                if (!gravityEffect.InPlanetGravity)
                {
                    EnterSpaceState();
                }
                else if (data.InDialog)
                {
                    EnterDialogState();
                }
            }
            else if (_state == ProtagControllerState.InSpace)
            {
                AlignToUpVector(_rb.linearVelocity.normalized);

                if (gravityEffect.InPlanetGravity)
                {
                    ExitSpaceState();
                    EnterPlanetState();
                }
                else if (data.InDialog)
                {
                    ExitSpaceState();
                    EnterDialogState();
                }
            }
            else if (_state == ProtagControllerState.InDialog)
            {
                HorizontalMovement(0, groundNormal);
                AlignToUpVector(planetGravityUp);

                if (!data.InDialog)
                {
                    if (gravityEffect.InPlanetGravity)
                    {
                        EnterPlanetState();
                    }
                    else
                    {
                        EnterSpaceState();
                    }
                }
            }

            void EnterSpaceState()
            {
                _state = ProtagControllerState.InSpace;
                _protagCamera.SetAlignRotation(false);

                if (!state.ContainsReplayed())
                {
                    _onEnterSpace.Invoke();
                }
            }

            void ExitSpaceState()
            {
                if (!state.ContainsReplayed())
                {
                    _onExitSpace.Invoke();
                }
            }

            void EnterPlanetState()
            {
                _state = ProtagControllerState.InPlanet;
                _protagCamera.SetAlignRotation(true);
            }

            void EnterDialogState()
            {
                _state = ProtagControllerState.InDialog;
                _protagCamera.SetAlignRotation(true);
            }

            void AlignToUpVector(Vector2 targetUp)
            {
                Quaternion targetRot = Quaternion.FromToRotation(Vector2.up, targetUp);
                Quaternion currentRot = _bodyAnchor.rotation;

                // Use lerp first then linear to get smooth rotations for sudden flips while still sticking on incremental rotations
                float t = 1 - Mathf.Pow(0.01f, delta * _moveStats.AlignLerpExp);
                currentRot = Quaternion.Lerp(
                    currentRot,
                    targetRot,
                    t);

                currentRot = Quaternion.RotateTowards(
                    currentRot,
                    targetRot,
                    _moveStats.AlignAngleVelocity * delta);

                _bodyAnchor.rotation = currentRot;
            }

            void HorizontalMovement(float horizontalInput, Vector2 normal)
            {
                // In planet horizontal movement
                Vector2 currentHorizontal = _rb.linearVelocity.ProjectOnPlane(normal);
                Vector2 groundTangent = Vector2.Perpendicular(normal);
                Vector2 desiredHorizontal = -horizontalInput * _moveStats.MoveSpeed * groundTangent;
                float accel = isGrounded
                    ? _moveStats.GroundAccel
                    : _moveStats.AirMoveAccel;

                Vector2 newHorizontal =
                    Vector2.MoveTowards(currentHorizontal, desiredHorizontal, accel * delta);

                Vector2 moveDelta = newHorizontal - currentHorizontal;
                Vector2 newVelocity = _rb.linearVelocity + moveDelta;

                _predictionRigidbody.Velocity(newVelocity);
            }

            // Clamp the velocity to the max possible speed
            if (_rb.linearVelocity.magnitude > _moveStats.MaxPossibleSpeed)
            {
                _predictionRigidbody.Velocity(
                    _rb.linearVelocity.normalized * _moveStats.MaxPossibleSpeed);
            }

            bool velIsDown = Vector2.Dot(_rb.linearVelocity, planetGravityUp) <= 0;
            bool applyGravity = !(isGrounded && horizontal == 0 && velIsDown);

            if (applyGravity)
            {
                _predictionRigidbody.AddForce(totalGravAccel * delta * _rb.mass,
                    ForceMode2D.Impulse);
                // BadLogger.LogTrace(totalGravAccel.ToString());
            }

            _predictionRigidbody.Simulate();

            if (state.ContainsCreated())
            {
                ReplicateVisuals((int)horizontal, isGrounded);
            }
        }

        private void ReplicateVisuals(int horizontalInput, bool isGrounded)
        {
            if (_state != ProtagControllerState.InDialog)
            {
                if (horizontalInput > 0)
                {
                    _spriteRenderer.flipX = false;
                }
                else if (horizontalInput < 0)
                {
                    _spriteRenderer.flipX = true;
                }

                _animator.SetFloat(SpeedAnimHash, _moveStats.MoveSpeed * Mathf.Abs(horizontalInput));
            }
            else
            {
                _animator.SetFloat(SpeedAnimHash, 0);
            }

            _animator.SetBool(AirAnimHash, !isGrounded);
        }

        public override void CreateReconcile()
        {
            if (!IsServerStarted)
            {
                return;
            }

            var data = new ReconcileData(_predictionRigidbody, _state, _horizontalInput);
            Reconcile(data);
        }

        [Reconcile]
        private void Reconcile(ReconcileData data, Channel channel = Channel.Unreliable)
        {
            _predictionRigidbody.Reconcile(data.Rigidbody2DState);
            ReplicateVisuals((int)data.HorizontalInput, true);
            _state = data.State;
            // BadLogger.LogTrace($"A {data.GetTick()} {name}");
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