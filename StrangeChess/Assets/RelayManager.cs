using System.Collections.Generic;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Core.Environments; 
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using TMPro;

public class RelayManager : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text statusText; // Replaces your old codeDisplayText
    public GameObject startGameButton; 
    
    // We no longer need the InputField!

    private Lobby currentLobby;
    private float heartbeatTimer;

    async void Start()
    {
        if (startGameButton != null) startGameButton.SetActive(false);
        statusText.text = "Connecting to Unity Services...";

        InitializationOptions options = new InitializationOptions();

#if UNITY_EDITOR
        if (ParrelSync.ClonesManager.IsClone())
        {
            options.SetProfile("ClonePlayer");
        }
        else
        {
            options.SetProfile("PrimaryPlayer");
        }
#endif

        await UnityServices.InitializeAsync(options);

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            statusText.text = "Ready to play!";
        }
    }

    private void Update()
    {
        // Lobbies shut down if the host doesn't ping them every 30 seconds.
        // We will send a heartbeat ping every 15 seconds to keep it alive.
        if (currentLobby != null && currentLobby.HostId == AuthenticationService.Instance.PlayerId)
        {
            heartbeatTimer += Time.deltaTime;
            if (heartbeatTimer > 15f)
            {
                heartbeatTimer = 0f;
                LobbyService.Instance.SendHeartbeatPingAsync(currentLobby.Id);
            }
        }
    }

    public async void StartHost()
    {
        try
        {
            statusText.text = "Creating room...";

            // 1. Create Relay Allocation
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(1);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            // 2. Configure Transport
            RelayServerData relayServerData = new RelayServerData(allocation, "dtls");
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);

            // 3. Create Lobby and hide the Relay Code inside it
            CreateLobbyOptions lobbyOptions = new CreateLobbyOptions
            {
                IsPrivate = false,
                Data = new Dictionary<string, DataObject>
                {
                    { "RelayCode", new DataObject(DataObject.VisibilityOptions.Member, joinCode) }
                }
            };
            
            currentLobby = await LobbyService.Instance.CreateLobbyAsync("VR Chess Room", 2, lobbyOptions);
            
            // 4. Start Host
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.StartHost();
            
            statusText.text = "Waiting for an opponent...";
        }
        catch (System.Exception e)
        {
            Debug.LogError(e);
            statusText.text = "Failed to create room.";
        }
    }

    public async void QuickJoinGame()
    {
        try
        {
            statusText.text = "Searching for a room...";

            // 1. Find any open lobby
            currentLobby = await LobbyService.Instance.QuickJoinLobbyAsync();

            // 2. Extract the Relay code
            string relayCode = currentLobby.Data["RelayCode"].Value;
            
            // 3. Join Relay
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(relayCode);
            RelayServerData relayServerData = new RelayServerData(joinAllocation, "dtls");
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);

            NetworkManager.Singleton.StartClient();
            statusText.text = "Joined successfully!";
        }
        catch (LobbyServiceException e)
        {
            Debug.LogWarning(e);
            statusText.text = "No open rooms found. Try Hosting!";
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        if (NetworkManager.Singleton.IsServer && NetworkManager.Singleton.ConnectedClientsList.Count == 2)
        {
            statusText.text = "Opponent joined!";
            startGameButton.SetActive(true);
        }
    }

    public async void LoadGameScene()
    {
        if (NetworkManager.Singleton.IsServer)
        {
            // Lock or delete the lobby so no one else tries to join while you are playing
            if (currentLobby != null)
            {
                await LobbyService.Instance.DeleteLobbyAsync(currentLobby.Id);
            }

            NetworkManager.Singleton.SceneManager.LoadScene("Chess", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
    }
}