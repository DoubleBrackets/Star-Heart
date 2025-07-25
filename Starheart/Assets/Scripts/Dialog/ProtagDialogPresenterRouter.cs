using System;
using System.Threading;
using DebugTools.Logging;
using UnityEngine;
using Yarn.Unity;

namespace Dialog
{
    /// <summary>
    ///     Routes dialogue lines to the networked presenters
    /// </summary>
    public class ProtagDialogPresenterRouter : DialoguePresenterBase
    {
        private const string ProtagCharacterName = "Protag";

        [SerializeField]
        private NetworkedLinePresenter _protagLinePresenter;

        public event Action OnEndDialogue;

        private NetworkedLinePresenter _npcLinePresenter;

        public void SetNpcLinePresenter(NetworkedLinePresenter presenter)
        {
            _npcLinePresenter = presenter;
        }

        public override async YarnTask RunLineAsync(LocalizedLine line, LineCancellationToken token)
        {
            // Select between protag and NPC line presenters 
            if (line.CharacterName == ProtagCharacterName)
            {
                _protagLinePresenter.RunLineAsync(line, token);
                _protagLinePresenter.ShowDialogueBubble();
            }
            else
            {
                if (_npcLinePresenter == null)
                {
                    BadLogger.LogWarning("NPC Line Presenter is not set");
                }
                else
                {
                    _npcLinePresenter.RunLineAsync(line, token);
                    _npcLinePresenter.ShowDialogueBubble();
                }
            }

            await YarnTask.WaitUntilCanceled(token.NextLineToken).SuppressCancellationThrow();
        }

        public override YarnTask<DialogueOption> RunOptionsAsync(DialogueOption[] dialogueOptions,
            CancellationToken cancellationToken)
        {
            // Hide locally since options are showing, but not for others (since they don't see options)
            _protagLinePresenter.SetDialogBubbleLocal(false);
            return YarnTask<DialogueOption?>.FromResult(null);
        }

        public override YarnTask OnDialogueStartedAsync()
        {
            return YarnTask.CompletedTask;
        }

        public override YarnTask OnDialogueCompleteAsync()
        {
            _protagLinePresenter.HideDialogueBubble();

            if (_npcLinePresenter != null)
            {
                _npcLinePresenter.HideDialogueBubble();
            }

            OnEndDialogue?.Invoke();

            return YarnTask.CompletedTask;
        }
    }
}