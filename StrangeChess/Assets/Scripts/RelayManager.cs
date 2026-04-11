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
using UnityEngine.SceneManagement;

public class RelayManager : MonoBehaviour
{
    [Header("Matchmaking Settings")]
    [Tooltip("If true, the game will automatically find a room or create one on startup.")]
    public bool autoConnect = true;

    [Header("UI References")]
    public TMP_Text statusText; 
    public GameObject startGameButton; // You can leave this unassigned or delete it from the Canvas now

    private Lobby currentLobby;
    private float heartbeatTimer;

    public void changeSceneToChess()
    {
        SceneManager.LoadScene("Chess");
    }

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
            
            // If AutoConnect is checked in the Inspector, start matchmaking immediately!
            if (autoConnect)
            {
                AutoJoinOrHost();
            }
            else
            {
                statusText.text = "Ready to play! Select Host or Join.";
            }
        }
    }

    private void Update()
    {
        // Lobbies shut down if the host doesn't ping them every 30 seconds.
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

    // --- AUTOMATIC MATCHMAKING ---

    public async void AutoJoinOrHost()
    {
        statusText.text = "Looking for an opponent...";
        
        try
        {
            // 1. Try to find any open lobby first
            currentLobby = await LobbyService.Instance.QuickJoinLobbyAsync();

            // If we succeed, extract the code and join as a Client
            string relayCode = currentLobby.Data["RelayCode"].Value;
            
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(relayCode);
            RelayServerData relayServerData = new RelayServerData(joinAllocation, "dtls");
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);

            NetworkManager.Singleton.StartClient();
            statusText.text = "Match found! Joining game...";
        }
        catch (LobbyServiceException)
        {
            // 2. If QuickJoin fails (no lobbies found), we become the Host instead!
            Debug.Log("No open rooms found. Automatically hosting a new room...");
            StartHost();
        }
    }

    // --- MANUAL MATCHMAKING (Fallback) ---

    public async void StartHost()
    {
        try
        {
            statusText.text = "Creating room...";

            // Create Relay Allocation
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(1);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            // Configure Transport
            RelayServerData relayServerData = new RelayServerData(allocation, "dtls");
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);

            // Create Lobby and hide the Relay Code inside it
            CreateLobbyOptions lobbyOptions = new CreateLobbyOptions
            {
                IsPrivate = false,
                Data = new Dictionary<string, DataObject>
                {
                    { "RelayCode", new DataObject(DataObject.VisibilityOptions.Member, joinCode) }
                }
            };
            
            currentLobby = await LobbyService.Instance.CreateLobbyAsync("VR Chess Room", 2, lobbyOptions);
            
            // Start Host
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.StartHost();
            
            statusText.text = "Waiting for an opponent to join...";
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
            currentLobby = await LobbyService.Instance.QuickJoinLobbyAsync();
            string relayCode = currentLobby.Data["RelayCode"].Value;
            
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

    // --- GAME START LOGIC ---

    private void OnClientConnected(ulong clientId)
    {
        // When 2 players are in, trigger the hack
        if (NetworkManager.Singleton.IsServer && NetworkManager.Singleton.ConnectedClientsList.Count == 2)
        {
            // HACK 1: Make this object immortal. If the Host loads the scene too fast, 
            // this script usually gets murdered midway. This stops that.
            DontDestroyOnLoad(this.gameObject);
            
            statusText.text = "Opponent found! Forcing connection...";
            
            // HACK 2: Start a brute-force Coroutine
            StartCoroutine(ForceLoadSceneHack());
        }
    }

    private System.Collections.IEnumerator ForceLoadSceneHack()
    {
        // HACK 3: THE MAGIC DELAY. We freeze the Host for 3 full seconds. 
        // This gives the Client all the time in the world to finish its Relay handshake.
        yield return new WaitForSeconds(3f);
        
        statusText.text = "Dragging Client to Arena!";
        
        // Fire the official network scene change
        NetworkManager.Singleton.SceneManager.LoadScene("Chess", UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    public async void LoadGameScene()
    {
        if (NetworkManager.Singleton.IsServer)
        {
            // Delete the lobby so no one else tries to join while you are playing
            if (currentLobby != null)
            {
                await LobbyService.Instance.DeleteLobbyAsync(currentLobby.Id);
            }

            // Load the actual gameplay scene
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