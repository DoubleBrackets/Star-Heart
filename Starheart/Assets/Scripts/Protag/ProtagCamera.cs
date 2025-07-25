using FishNet.Object;
using Protag;
using Unity.Cinemachine;
using UnityEngine;

namespace Gameplay
{
    public class ProtagCamera : NetworkBehaviour
    {
        [SerializeField]
        private CinemachineCamera _cinemachineCamera;

        [SerializeField]
        private float _planetFov;

        [SerializeField]
        private float _spaceFov;

        [SerializeField]
        private float _fovLerpExp;

        [SerializeField]
        private CinemachineRotateWithFollowTarget _rotateWithFollowTarget;

        private float _targetFov;

        private void Update()
        {
            if (!IsOwner)
            {
                return;
            }

            // Smoothly transition to the target FOV
            float t = 1 - Mathf.Pow(0.01f, Time.deltaTime * _fovLerpExp);
            _cinemachineCamera.Lens.OrthographicSize = Mathf.Lerp(_cinemachineCamera.Lens.OrthographicSize, _targetFov,
                t);
        }

        public override void OnStartClient()
        {
            _targetFov = _cinemachineCamera.Lens.OrthographicSize;
            _cinemachineCamera.enabled = IsOwner;
        }

        public void SetAlignRotation(bool align)
        {
            if (_rotateWithFollowTarget != null)
            {
                _rotateWithFollowTarget.enabled = align;
            }
            else
            {
                Debug.LogWarning("RotateWithFollowTarget component is not assigned.");
            }
        }

        public void SetCamera(ProtagController.ProtagControllerState state)
        {
            if (!IsOwner)
            {
                return;
            }

            switch (state)
            {
                case ProtagController.ProtagControllerState.InSpace:
                    _targetFov = _spaceFov;
                    break;
                case ProtagController.ProtagControllerState.InPlanet:
                    _targetFov = _planetFov;
                    break;
                default:
                    Debug.LogWarning("Unknown ProtagController state: " + state);
                    break;
            }
        }
    }
}