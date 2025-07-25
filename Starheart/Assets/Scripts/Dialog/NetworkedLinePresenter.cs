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

        private readonly SyncVar<string> _currentLine = new();
        private readonly SyncVar<string> _currentSpeaker = new();

        private readonly SyncVar<bool> _isVisible = new();

        private void Awake()
        {
            SetDialogBubbleLocal(false);
            _currentLine.OnChange += OnLineChanged;
            _currentSpeaker.OnChange += OnSpeakerChanged;
            _isVisible.OnChange += HandleVisibilityChanged;
        }

        private void OnDestroy()
        {
            _currentLine.OnChange -= OnLineChanged;
            _currentSpeaker.OnChange -= OnSpeakerChanged;
            _isVisible.OnChange -= HandleVisibilityChanged;
        }

        private void HandleVisibilityChanged(bool prev, bool next, bool asserver)
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = next ? 1 : 0;
                _canvasGroup.interactable = next;
                _canvasGroup.blocksRaycasts = next;
            }
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
            HandleVisibilityChanged(false, visible, false);
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
            Debug.Log("SERVER SET VISIBLE" + value);
            _isVisible.Value = value;
        }
    }
}