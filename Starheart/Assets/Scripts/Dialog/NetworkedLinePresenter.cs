using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using TMPro;
using UnityEngine;
using Yarn.Unity;

namespace Dialog
{
    public class NetworkedLinePresenter : NetworkBehaviour
    {
        [SerializeField]
        private CanvasGroup _canvasGroup;

        [SerializeField]
        private TMP_Text _text;

        [SerializeField]
        private TMP_Text _speakerText;

        public event Action<bool> OnInDialogChanged;

        private readonly SyncVar<string> _currentLine = new();
        private readonly SyncVar<string> _currentSpeaker = new();

        private void Awake()
        {
            SetDialogBubbleLocal(false);
            _currentLine.OnChange += OnLineChanged;
            _currentSpeaker.OnChange += OnSpeakerChanged;
        }

        private void OnDestroy()
        {
            _currentLine.OnChange -= OnLineChanged;
            _currentSpeaker.OnChange -= OnSpeakerChanged;
        }

        private void OnLineChanged(string prev, string next, bool asserver)
        {
            if (_text != null)
            {
                _text.text = next;
            }
        }

        private void OnSpeakerChanged(string prev, string next, bool asserver)
        {
            if (_speakerText != null)
            {
                _speakerText.text = next;
            }
        }

        public YarnTask RunLineAsync(LocalizedLine line, LineCancellationToken token)
        {
            RunLine_RPC(line.TextWithoutCharacterName.Text, line.CharacterName);
            return YarnTask.CompletedTask;
        }

        public void ShowDialogueBubble()
        {
            SetVisible_ServerRpc(true);
        }

        public void HideDialogueBubble()
        {
            SetVisible_ServerRpc(false);
        }

        public void SetDialogBubbleLocal(bool visible)
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = visible ? 1 : 0;
                _canvasGroup.interactable = visible;
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void RunLine_RPC(string line, string speaker)
        {
            _currentLine.Value = line;
            _currentSpeaker.Value = speaker;
        }

        [ServerRpc(RequireOwnership = false)]
        private void SetVisible_ServerRpc(bool value)
        {
            OnInDialogChanged?.Invoke(value);
            SetVisible_ObserversRpc(value);
        }

        [ObserversRpc(RunLocally = true, BufferLast = true)]
        private void SetVisible_ObserversRpc(bool visible)
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = visible ? 1 : 0;
                _canvasGroup.interactable = visible;
            }
        }
    }
}