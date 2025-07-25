using DebugTools.Logging;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using TMPro;
using UnityEngine;

namespace Environment
{
    public class StardustManager : NetworkBehaviour
    {
        private static StardustManager _instance;

        [SerializeField]
        private TMP_Text _stardustCollectedText;

        public static StardustManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<StardustManager>();
                }

                return _instance;
            }
        }

        private readonly SyncVar<int> _stardustCollectedCount = new();
        private readonly SyncVar<int> _totalStardustCount = new();

        private void Awake()
        {
            _instance = this;
            _stardustCollectedCount.OnChange += UpdateText;
            _totalStardustCount.OnChange += UpdateText;
        }

        private void Update()
        {
            _stardustCollectedText.text = $"{_stardustCollectedCount.Value} / {_totalStardustCount.Value}";
        }

        public void OnDestroy()
        {
            _stardustCollectedCount.OnChange -= UpdateText;
            _totalStardustCount.OnChange -= UpdateText;
        }

        public override void OnStartServer()
        {
            _totalStardustCount.Value = FindObjectsByType<Stardust>(FindObjectsSortMode.None).Length;
            UpdateText(0, 0, false);
        }

        public override void OnStartClient()
        {
            UpdateText(0, 0, false);
        }

        [Server]
        public void RegisterStardust()
        {
            // _totalStardustCount.Value++;
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
            _stardustCollectedText.text = $"{_stardustCollectedCount.Value} / {_totalStardustCount.Value}";
        }
    }
}