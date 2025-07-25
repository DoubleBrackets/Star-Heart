using DebugTools.Logging;
using Dialog;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using Yarn.Unity;

namespace Protag
{
    public class ProtagDialogManager : NetworkBehaviour
    {
        [SerializeField]
        private GameObject _talkPopup;

        [SerializeField]
        private ProtagDialogPresenterRouter _dialogPresenterRouter;

        [SerializeField]
        private DialogueRunner _dialogueRunner;

        [SerializeField]
        private ProtagController _protagController;

        [SerializeField]
        private HeartStarThrower _heartStarThrower;

        private readonly SyncVar<bool> _inDialog = new();

        private bool _inDialogZone;

        private DialogPoint _currentDialogPoint;

        private void Update()
        {
            if (!IsOwner)
            {
                return;
            }

            // Check for dialog input
            if (_inDialogZone && !_inDialog.Value && Input.GetKeyDown(KeyCode.E))
            {
                TryStartDialog_ServerRpc(_currentDialogPoint);
            }

            if (_inDialog.Value && Input.GetKeyDown(KeyCode.E))
            {
                _dialogueRunner.RequestNextLine();
            }

            UpdateTalkPopup();
        }

        public override void OnStartClient()
        {
            if (IsOwner)
            {
                _dialogPresenterRouter.OnEndDialogue += HandleDialogEnd;
                _inDialog.OnChange += HandleInDialogChanged;
            }
        }

        [Client]
        private void HandleDialogEnd()
        {
            OnStopDialog_ServerRpc(_currentDialogPoint);
        }

        public override void OnStopClient()
        {
            if (IsOwner)
            {
                OnStopDialog_ServerRpc(_currentDialogPoint);
                _dialogPresenterRouter.OnEndDialogue -= HandleDialogEnd;
                _inDialog.OnChange -= HandleInDialogChanged;
            }
        }

        private void HandleInDialogChanged(bool prev, bool next, bool asserver)
        {
            UpdateTalkPopup();

            if (IsOwner)
            {
                _protagController.InDialog = next;
                _heartStarThrower.InputEnabled = !next;
            }
        }

        [Client]
        public void EnteredDialogZone(DialogPoint dialogPoint)
        {
            if (!IsOwner)
            {
                return;
            }

            if (dialogPoint.IsInDialog)
            {
                return;
            }

            _currentDialogPoint = dialogPoint;
            _inDialogZone = true;
            UpdateTalkPopup();
        }

        [Client]
        public void ExitedDialogZone()
        {
            if (!IsOwner)
            {
                return;
            }

            _inDialogZone = false;
            UpdateTalkPopup();
        }

        [ServerRpc]
        private void TryStartDialog_ServerRpc(DialogPoint dialogPoint)
        {
            if (!_inDialog.Value && !dialogPoint.IsInDialog)
            {
                BadLogger.LogInfo($"Starting dialog with {dialogPoint.name} on server");
                _inDialog.Value = true;
                dialogPoint.SetInDialog(true);
                StartDialog_TargetRpc(Owner, dialogPoint);
            }
            else
            {
                BadLogger.LogInfo("Tried to start dialog while already in dialog");
            }
        }

        [TargetRpc]
        private void StartDialog_TargetRpc(NetworkConnection conn, DialogPoint dialogPoint)
        {
            // As long as the starting conditions are valid, the entire dialog is run client side
            // with only the text being sent to the server and shared
            _dialogPresenterRouter.SetNpcLinePresenter(dialogPoint.LinePresenter);
            _dialogueRunner.StartDialogue(dialogPoint.StartNode);

            BadLogger.LogInfo($"Starting dialog on client with {dialogPoint.name}");
        }

        [ServerRpc]
        private void OnStopDialog_ServerRpc(DialogPoint dialogPoint)
        {
            if (_inDialog.Value)
            {
                BadLogger.LogInfo($"Ending dialog with {dialogPoint.name} on server");
                _inDialog.Value = false;
                dialogPoint.SetInDialog(false);
                _dialogueRunner.Stop();
            }
        }

        [Client]
        private void UpdateTalkPopup()
        {
            bool currentDialogPointOpen = _currentDialogPoint != null && !_currentDialogPoint.IsInDialog;
            _talkPopup.SetActive(_inDialogZone && !_inDialog.Value && currentDialogPointOpen);
        }
    }
}