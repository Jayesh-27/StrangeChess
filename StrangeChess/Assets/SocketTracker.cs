using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using Unity.Netcode;

public class SocketTracker : MonoBehaviour
{
    public XRSocketInteractor socket;
    public int Square = 0;
    [SerializeField] ChessManager chessManager;

    void Start()
    {
        socket = GetComponent<XRSocketInteractor>();

        socket.selectEntered.AddListener(OnSnap);
        socket.selectExited.AddListener(OnUnsnap);

        if (socket.hasSelection)
        {
            Debug.Log("Something is already in socket at square: " + Square);
        }
    }

    void OnSnap(SelectEnterEventArgs args)
    {
        Debug.Log(args.interactableObject.transform.name + " Snapped to square: " + Square);
        chessManager.shouldSnapBack = false;
        chessManager.enableAllSockets();

        // --- NEW NETWORK SYNC ---
        // Tell the opponent to snap this piece into this exact square!
        NetworkObject netObj = args.interactableObject.transform.GetComponent<NetworkObject>();
        
        // We only send the network message if WE are the one holding the piece.
        if (netObj != null && netObj.IsOwner)
        {
            chessManager.SyncSnapServerRpc(netObj.NetworkObjectId, Square);
        }
    }

    void OnUnsnap(SelectExitEventArgs args)
    {
        chessManager.shouldSnapBack = true;
        chessManager.lastUnsnap = this;
        Debug.Log(args.interactableObject.transform.name + " Removed from square: " + Square);
    }
}