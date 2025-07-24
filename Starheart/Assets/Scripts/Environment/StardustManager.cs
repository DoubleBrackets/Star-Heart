using DebugTools.Logging;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using TMPro;
using UnityEngine;

namespace Environment
{
    public class StardustManager : NetworkBehaviour
    {
        [SerializeField]
        private TMP_Text _stardustCollectedText;
        public static StardustManager Instance { get; private set; }
        
        private readonly SyncVar<int> _stardustCollectedCount = new SyncVar<int>();
        private readonly SyncVar<int> _totalStardustCount = new SyncVar<int>();
        
        void Awake()
        {
            Instance = this;
        }

        public override void OnStartNetwork()
        {
            _stardustCollectedCount.OnChange += UpdateText;
            _totalStardustCount.OnChange += UpdateText;

            UpdateText(0,0,false);
        }

        public override void OnStopNetwork()
        {
            _stardustCollectedCount.OnChange -= UpdateText;
            _totalStardustCount.OnChange -= UpdateText;

            if (Instance == this)
            {
                Instance = null;
            }
        }

        [Server]
        public void RegisterStardust()
        {
            _totalStardustCount.Value++;
            BadLogger.LogDebug($"Stardust registered. Total count: {_totalStardustCount.Value}");
        }
        
        [Server]
        public void CollectStardust(Stardust stardust)
        {
            _stardustCollectedCount.Value++;
            BadLogger.LogDebug($"Stardust collected. Total count: {_stardustCollectedCount.Value}");
        }

        private void UpdateText(int prev, int next, bool asServer)
        {
            if (_stardustCollectedText != null)
            {
                _stardustCollectedText.text = $"{_stardustCollectedCount.Value} / {_totalStardustCount.Value}";
            }
            else
            {
                Debug.LogWarning("Stardust text UI is not assigned.");
            }
        }
    }
}
