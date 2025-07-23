using FishNet.Object;
using Unity.Cinemachine;
using UnityEngine;

namespace Gameplay
{
    public class ProtagCamera : NetworkBehaviour
    {
        [SerializeField]
        private CinemachineCamera _cinemachineCamera;

        public override void OnStartClient()
        {
            _cinemachineCamera.enabled = IsOwner;
        }
    }
}