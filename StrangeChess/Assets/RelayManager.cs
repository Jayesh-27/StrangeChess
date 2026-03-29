using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Core.Environments; // Required for ParrelSync profile support
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using TMPro;

public class RelayManager : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text codeDisplayText;
    public TMP_InputField codeInputField;

    [Header("Lobby UI")]
    public GameObject startGameButton; // Assign your new Start Game button here

    async void Start()
    {
        // Make sure the start game button is hidden when the game first boots up
        if (startGameButton != null)
        {
            startGameButton.SetActive(false);
        }

        // Create setup options for Unity Services to handle ParrelSync profiles
        InitializationOptions options = new InitializationOptions();

#if UNITY_EDITOR
        // If this is the ParrelSync clone, force it to use a different login profile
        if (ParrelSync.ClonesManager.IsClone())
        {
            options.SetProfile("ClonePlayer");
        }
        else
        {
            options.SetProfile("PrimaryPlayer");
        }
#endif

        // 1. Authenticate players anonymously so they can use Relay (using our ParrelSync options)
        await UnityServices.InitializeAsync(options);

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log("Signed in to Unity Services. Player ID: " + AuthenticationService.Instance.PlayerId);
        }
    }

    public async void StartHost()
    {
        try
        {
            // 2. Request a Relay server for 2 players (Host + 1 Client)
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(1);

            // 3. Get the join code to share with the client
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            codeDisplayText.text = "Tell your friend this code: " + joinCode;

            // 4. Configure the Network Transport
            RelayServerData relayServerData = new RelayServerData(allocation, "dtls");
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);

            // 5. Start listening for when clients connect BEFORE starting the host
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

            // 6. Start the Host
            NetworkManager.Singleton.StartHost();
        }
        catch (RelayServiceException e)
        {
            Debug.LogError(e);
        }
    }

    // This function automatically runs every time ANY player connects
    private void OnClientConnected(ulong clientId)
    {
        // Check if WE are the server, and if there are exactly 2 people connected (The Host + The Client)
        if (NetworkManager.Singleton.IsServer && NetworkManager.Singleton.ConnectedClientsList.Count == 2)
        {
            Debug.Log("Client has joined! Showing Start Button.");
            startGameButton.SetActive(true);
        }
    }

    // Link this function to your new "Start Game" button's OnClick event!
    public void LoadGameScene()
    {
        if (NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.SceneManager.LoadScene("Chess", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }

    public async void StartClient()
    {
        try
        {
            // 7. Join the Relay server using the code from the input field
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(codeInputField.text);

            // 8. Configure Transport and connect
            RelayServerData relayServerData = new RelayServerData(joinAllocation, "dtls");
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);

            NetworkManager.Singleton.StartClient();
        }
        catch (RelayServiceException e)
        {
            Debug.LogError(e);
        }
    }

    // Clean up the event listener when this object is destroyed to prevent memory leaks
    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
    }
}