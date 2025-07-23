using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DebugTools.Logging;
using FishNet;
using FishNet.Transporting.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace UnityMultiplayer
{
    public class UnityCloudManager : MonoBehaviour
    {
        /// <summary>
        ///     Data about a join allocation event, for use with UI or other systems.
        /// </summary>
        public struct JoinAllocationEventData
        {
            public bool DidSucceed;
            public string JoinCode;
            public string FailureReason;

            public JoinAllocationEventData(bool didSucceed, string joinCode = "", string failureReason = "")
            {
                DidSucceed = didSucceed;
                JoinCode = joinCode;
                FailureReason = failureReason;
            }
        }

        /// <summary>
        ///     Data about a create allocation event, for use with UI or other systems.
        /// </summary>
        public struct CreateAllocationEventData
        {
            public bool DidSucceed;
            public string FailureReason;
            public string JoinCode;

            public CreateAllocationEventData(bool result, string failureReason = "", string joinCode = "")
            {
                JoinCode = joinCode;
                DidSucceed = result;
                FailureReason = failureReason;
            }
        }

        public struct AuthenticationEventData
        {
            public bool IsAuthenticated;
            public string ErrorMessage;
            public int Attempts;

            public AuthenticationEventData(bool isAuthenticated, int attempts, string errorMessage = "")
            {
                IsAuthenticated = isAuthenticated;
                ErrorMessage = errorMessage;
                Attempts = attempts;
            }
        }

        public const int MaxPlayers = 2;

        [Header("Depends")]

        [SerializeField]
        private UnityTransport _fishyUnityTransport;

        public static UnityCloudManager Instance { get; private set; }

        public string JoinCode { get; private set; }
        public bool IsInitialized { get; private set; }

        public event Action<JoinAllocationEventData> OnJoinAllocationEvent;
        public event Action<CreateAllocationEventData> OnCreateAllocationEvent;
        public event Action<AuthenticationEventData> OnInitialized;

        private bool _isInAllocation;

        private int _attempts;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            Initialize(gameObject.GetCancellationTokenOnDestroy()).Forget();
        }

        private async UniTaskVoid Initialize(CancellationToken token)
        {
            while (UnityServices.State == ServicesInitializationState.Uninitialized ||
                   !AuthenticationService.Instance.IsSignedIn)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    await Authenticate();
                    BadLogger.Log("Unity Services initialized and authenticated successfully.");
                    OnInitialized?.Invoke(new AuthenticationEventData(true, _attempts, "Authenticated"));
                    IsInitialized = true;
                }
                catch (Exception e)
                {
                    BadLogger.LogError($"Failed to authenticate: {e.Message}");
                    _attempts++;
                    OnInitialized?.Invoke(new AuthenticationEventData(false, _attempts,
                        $"{e.Message}"));
                }

                await UniTask.WaitForSeconds(5f, cancellationToken: token);
            }
        }

        private async UniTask Authenticate()
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                var options = new InitializationOptions();

                await UnityServices.InitializeAsync(options);
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
        }

        public async UniTask<List<Region>> GetRegionList()
        {
            try
            {
                List<Region> result = await RelayService.Instance.ListRegionsAsync();
                return result;
            }
            catch (Exception e)
            {
                BadLogger.LogError(e.Message);
                return null;
            }
        }

        public async UniTaskVoid HostGame(string regionId, CancellationToken token)
        {
            if (_isInAllocation || InstanceFinder.ClientManager.Started)
            {
                BadLogger.LogWarning("Already in an allocation or client is started.");
                return;
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                BadLogger.LogWarning("Not authenticated. Please sign in before hosting a game.");
                return;
            }

            _isInAllocation = true;

            try
            {
                Allocation allocation = await AllocateRelay();

                if (allocation == default)
                {
                    OnCreateAllocationEvent?.Invoke(new CreateAllocationEventData(false, "Failed to allocate relay."));
                    _isInAllocation = false;
                    throw new Exception("Failed to allocate relay.");
                }

                string joinCode = await GetRelayJoinCode(allocation);
                JoinCode = joinCode;

                // Copy to clipboard
                GUIUtility.systemCopyBuffer = joinCode;

                BadLogger.LogInfo($"Relay allocated successfully. Join code: {joinCode}");

                InitializeFishnetHost(allocation);

                OnCreateAllocationEvent?.Invoke(new CreateAllocationEventData(true, joinCode: joinCode));
            }
            catch (Exception e)
            {
                BadLogger.LogError($"Error during allocation: {e.Message}");
                _fishyUnityTransport.Shutdown();
                OnCreateAllocationEvent?.Invoke(new CreateAllocationEventData(false, e.Message));
            }
            finally
            {
                _isInAllocation = false;
            }
        }

        private async UniTask<Allocation> AllocateRelay()
        {
            try
            {
                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(MaxPlayers);
                return allocation;
            }
            catch (RelayServiceException e)
            {
                BadLogger.LogError($"Failed to allocate relay: {e.Message}");
                return default;
            }
        }

        private async UniTask<string> GetRelayJoinCode(Allocation allocation)
        {
            try
            {
                string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                return joinCode;
            }
            catch (RelayServiceException e)
            {
                BadLogger.LogError($"Failed to get relay join code: {e.Message}");
                return default;
            }
        }

        public async UniTaskVoid JoinGame(string joinCode, CancellationToken token)
        {
            if (_isInAllocation || InstanceFinder.ClientManager.Started)
            {
                BadLogger.LogWarning("Already in an allocation or client is started.");
                return;
            }

            if (string.IsNullOrEmpty(joinCode) || joinCode.Length != 6)
            {
                BadLogger.LogWarning("Invalid join code. Please provide a valid 6-character join code.");
                return;
            }

            _isInAllocation = true;

            try
            {
                JoinAllocation joinAllocation = await JoinRelay(joinCode);

                if (joinAllocation == default)
                {
                    OnJoinAllocationEvent?.Invoke(new JoinAllocationEventData(false,
                        failureReason: "Failed to join relay."));
                    _isInAllocation = false;
                    throw new Exception("Failed to join relay.");
                }

                BadLogger.LogInfo($"Successfully joined relay with join code: {joinCode}");

                InitializeFishnetClient(joinAllocation);

                OnJoinAllocationEvent?.Invoke(new JoinAllocationEventData(true, joinCode));
            }
            catch (Exception e)
            {
                BadLogger.LogError($"Error during join: {e.Message}");
                _fishyUnityTransport.Shutdown();
                OnJoinAllocationEvent?.Invoke(new JoinAllocationEventData(false, failureReason: e.Message));
            }
            finally
            {
                _isInAllocation = false;
            }
        }

        private async UniTask<JoinAllocation> JoinRelay(string relayJoinCode)
        {
            try
            {
                JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(relayJoinCode);
                return joinAllocation;
            }
            catch (RelayServiceException e)
            {
                BadLogger.LogError($"Failed to join relay: {e.Message}");
                return default;
            }
        }

        private void InitializeFishnetHost(Allocation allocation)
        {
            ConfigureTransportType(out string connectionType);
            _fishyUnityTransport.SetRelayServerData(allocation.ToRelayServerData(connectionType));

            // Host is both server and client
            bool startServer = InstanceFinder.ServerManager.StartConnection();
            if (startServer)
            {
                InstanceFinder.ClientManager.StartConnection();
            }
        }

        private void InitializeFishnetClient(JoinAllocation joinAllocation)
        {
            ConfigureTransportType(out string connectionType);
            _fishyUnityTransport.SetRelayServerData(joinAllocation.ToRelayServerData(connectionType));
            InstanceFinder.ClientManager.StartConnection();
        }

        private void ConfigureTransportType(out string connectionType)
        {
            var isWebGL = false;
#if UNITY_WEBGL && !UNITY_EDITOR
            isWebGL = true;
#endif
            if (isWebGL)
            {
                BadLogger.LogInfo("WebGL; using wss");
                _fishyUnityTransport.UseWebSockets = true;
                connectionType = "wss";
            }
            else
            {
                BadLogger.LogInfo("Not webgl; using dtls");
                _fishyUnityTransport.UseWebSockets = false;
                connectionType = "dtls";
            }
        }
    }
}