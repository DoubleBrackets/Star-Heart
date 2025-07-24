using FishNet.Object;
using Unity.Cinemachine;
using UnityEngine;

namespace Gameplay
{
    public class ProtagCamera : NetworkBehaviour
    {
        [SerializeField]
        private CinemachineCamera _cinemachineCamera;

        [SerializeField]
        private CinemachineRotateWithFollowTarget _rotateWithFollowTarget;

        public override void OnStartClient()
        {
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
    }
}