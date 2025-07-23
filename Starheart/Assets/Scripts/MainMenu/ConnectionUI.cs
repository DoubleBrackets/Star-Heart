using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityMultiplayer;

namespace MainMenu
{
    public class ConnectionUI : MonoBehaviour
    {
        private const string autoRegion = "auto";

        [Header("Depends")]

        [SerializeField]
        private TMP_Text _statusText;

        [SerializeField]
        private TMP_InputField _joinCodeInput;

        [SerializeField]
        private TMP_Dropdown _regionDropdown;

        [SerializeField]
        private CanvasGroup _canvasGroup;

        private UnityCloudManager _cloudManager;

        private string _joinCode = string.Empty;

        private void Start()
        {
            _cloudManager = UnityCloudManager.Instance;

            _cloudManager.OnCreateAllocationEvent += OnCreateAllocationEvent;
            _cloudManager.OnJoinAllocationEvent += OnJoinAllocationEvent;
            _cloudManager.OnInitialized += HandleInitialized;

            _joinCodeInput.onValueChanged.AddListener(HandleJoinCodeInputChanged);

            _statusText.text = "Initializing...";
            _canvasGroup.alpha = 0.5f;
            _canvasGroup.interactable = false;

            if (UnityCloudManager.Instance.IsInitialized)
            {
                HandleInitialized();
            }
        }

        private void OnDestroy()
        {
            _cloudManager.OnCreateAllocationEvent -= OnCreateAllocationEvent;
            _cloudManager.OnJoinAllocationEvent -= OnJoinAllocationEvent;
            _cloudManager.OnInitialized -= HandleInitialized;

            _joinCodeInput.onValueChanged.RemoveListener(HandleJoinCodeInputChanged);
        }

        private void HandleInitialized(UnityCloudManager.AuthenticationEventData authenticationEventData)
        {
            if (authenticationEventData.IsAuthenticated)
            {
                HandleInitialized();
            }
            else
            {
                _statusText.text =
                    $"{authenticationEventData.ErrorMessage}. Retrying (Attempt {authenticationEventData.Attempts})...";
                _canvasGroup.alpha = 0.5f;
                _canvasGroup.interactable = false;
            }
        }

        private void HandleInitialized()
        {
            _statusText.text = "Fetching regions...";
            InitializeRegionDropdown().Forget();
            _statusText.text = "Host or Join an Existing Session";

            _canvasGroup.alpha = 1f;
            _canvasGroup.interactable = true;
        }

        private void OnCreateAllocationEvent(UnityCloudManager.CreateAllocationEventData data)
        {
            if (data.DidSucceed)
            {
                _statusText.text = $"Created allocation successfully. Join code: {data.JoinCode}";
            }
            else
            {
                _statusText.text = $"Failed to create allocation: {data.FailureReason}";
            }
        }

        private void OnJoinAllocationEvent(UnityCloudManager.JoinAllocationEventData data)
        {
            if (data.DidSucceed)
            {
                _statusText.text = "Joined allocation successfully";
            }
            else
            {
                _statusText.text = $"Failed to join allocation: {data.FailureReason}";
            }
        }

        private void HandleJoinCodeInputChanged(string value)
        {
            _joinCode = value.Trim();
        }

        private async UniTaskVoid InitializeRegionDropdown()
        {
            _regionDropdown.ClearOptions();
            List<Region> regions = await _cloudManager.GetRegionList();
            List<string> regionNames = regions.ConvertAll(region => region.Id);

            // QOS autodetect best region isn't available in WebGL
#if !UNITY_WEBGL || UNITY_EDITOR
            regionNames.Insert(0, autoRegion);
#endif

            _regionDropdown.AddOptions(regionNames);
            _regionDropdown.value = 0;
        }

        public void Host()
        {
            if (_cloudManager == null)
            {
                Debug.LogError("UnityCloudManager is not initialized.");
                return;
            }

            string region = _regionDropdown.options[_regionDropdown.value].text;
            if (region == autoRegion)
            {
                region = ""; // Use null for auto region selection
            }

            _cloudManager.HostGame(region, gameObject.GetCancellationTokenOnDestroy()).Forget();
        }

        public void Join()
        {
            if (_cloudManager == null)
            {
                Debug.LogError("UnityCloudManager is not initialized.");
                return;
            }

            _cloudManager.JoinGame(_joinCode, gameObject.GetCancellationTokenOnDestroy()).Forget();
        }
    }
}