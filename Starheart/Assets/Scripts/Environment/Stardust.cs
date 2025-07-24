using System;
using Cysharp.Threading.Tasks;
using DebugTools.Logging;
using FishNet.Component.Prediction;
using FishNet.Object;
using Protag;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace Environment
{
    public class Stardust : NetworkBehaviour
    {
        [SerializeField]
        private UnityEvent _onCollectEffects;
        
        [SerializeField]
        private UnityEvent _onCollected;
        
        [SerializeField]
        private NetworkTrigger2D _networkCollision2D;

        private bool _collected;
        
        public override void OnStartServer()
        {
            StardustManager.Instance.RegisterStardust();
        }

        private void Awake()
        {
            _networkCollision2D.OnEnter += NetworkCollisionEnter;
        }
        
        private void OnDestroy()
        {
            _networkCollision2D.OnEnter -= NetworkCollisionEnter;
        }

        private void NetworkCollisionEnter(Collider2D other)
        {
            var protag = other.GetComponentInParent<NetworkProtag>();
            
            
            if( protag == null || _collected)
            {
                return;
            }
            
            if (IsServerInitialized)
            {
                StardustManager.Instance.CollectStardust(this);
                OnCollected_RPC();
                DelayedDespawn().Forget();
            }

            if (!PredictionManager.IsReconciling)
            {
                _onCollectEffects?.Invoke();
            }
        }

        [ObserversRpc(RunLocally = true,BufferLast = true)]
        private void OnCollected_RPC()
        {
            _collected = true;
            _onCollected?.Invoke();
        }

        [Server]
        private async UniTaskVoid DelayedDespawn()
        {
            await UniTask.Delay(TimeSpan.FromSeconds(2f));
            Despawn(NetworkObject, DespawnType.Destroy);
        }
    }
}
