using FishNet.Component.Prediction;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Protag;
using UnityEngine;
using Yarn.Unity;
using Yarn.Unity.Attributes;

namespace Dialog
{
    public class DialogPoint : NetworkBehaviour
    {
        [YarnNode(nameof(yarnProject))]
        [SerializeField]
        private string _startNode;

        [SerializeField]
        private NetworkedLinePresenter _linePresenter;

        [SerializeField]
        private NetworkTrigger2D _networkCollision2D;

        [SerializeField]
        internal YarnProject? yarnProject;

        public string StartNode => _startNode;
        public NetworkedLinePresenter LinePresenter => _linePresenter;

        public bool IsInDialog => _isInDialog.Value;

        private readonly SyncVar<bool> _isInDialog = new();

        private void Awake()
        {
            _networkCollision2D.OnEnter += NetworkCollisionEnter;
            _networkCollision2D.OnExit += NetworkCollisionExit;
        }

        private void OnDestroy()
        {
            _networkCollision2D.OnEnter -= NetworkCollisionEnter;
            _networkCollision2D.OnExit += NetworkCollisionExit;
        }



        [Server]
        public void SetInDialog(bool inDialog)
        {
            _isInDialog.Value = inDialog;
        }

        private void NetworkCollisionEnter(Collider2D other)
        {
            var protagDialogStarter = other.GetComponentInParent<ProtagDialogStarter>();

            if (protagDialogStarter != null && protagDialogStarter.IsOwner)
            {
                if (!PredictionManager.IsReconciling)
                {
                    protagDialogStarter.EnteredDialogZone(this);
                }
            }
        }

        private void NetworkCollisionExit(Collider2D other)
        {
            var protagDialogStarter = other.GetComponentInParent<ProtagDialogStarter>();

            if (protagDialogStarter != null && protagDialogStarter.IsOwner)
            {
                if (!PredictionManager.IsReconciling)
                {
                    protagDialogStarter.ExitedDialogZone();
                }
            }
        }
    }
}