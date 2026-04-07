using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using Unity.Netcode;

public class SocketTracker : MonoBehaviour
{
    public XRSocketInteractor socket;
    public int Square = 0;
    [SerializeField] ChessManager chessManager;

    void Awake()
    {
        socket = GetComponent<XRSocketInteractor>();

        socket.selectEntered.AddListener(OnSnap);
        socket.selectExited.AddListener(OnUnsnap);
        
        // Listen for when a player hovers a piece over this socket!
        socket.hoverEntered.AddListener(OnHover); 
    }

    void OnHover(HoverEnterEventArgs args)
    {
        if (socket.hasSelection)
        {
            ChessPiece attackingPiece = args.interactableObject.transform.GetComponent<ChessPiece>();
            ChessPiece defendingPiece = socket.firstInteractableSelected.transform.GetComponent<ChessPiece>();

            if (attackingPiece != null && defendingPiece != null && attackingPiece.isWhitePiece != defendingPiece.isWhitePiece)
            {
                NetworkObject netObj = attackingPiece.GetComponent<NetworkObject>();
                
                if (netObj != null && netObj.IsOwner)
                {
                    Debug.Log($"[Capture] Banish enemy {defendingPiece.name} and snap {attackingPiece.name}!");
                    
                    // Tell the server WHO died, WHO killed them, and WHERE it happened
                    chessManager.CapturePieceServerRpc(
                        defendingPiece.GetComponent<NetworkObject>().NetworkObjectId, 
                        netObj.NetworkObjectId, 
                        Square
                    );
                }
            }
        }
    }

    void OnSnap(SelectEnterEventArgs args)
    {
        chessManager.shouldSnapBack = false;
        chessManager.enableAllSockets();

        // Update the piece's memory of where it currently is
        ChessPiece piece = args.interactableObject.transform.GetComponent<ChessPiece>();
        if (piece != null) piece.currentSquare = Square;

        NetworkObject netObj = args.interactableObject.transform.GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsOwner)
        {
            chessManager.SyncSnapServerRpc(netObj.NetworkObjectId, Square);
        }
    }

    void OnUnsnap(SelectExitEventArgs args)
    {
        chessManager.shouldSnapBack = true;
        
        // Save where the piece came from so we can check if it was a valid move later
        ChessPiece piece = args.interactableObject.transform.GetComponent<ChessPiece>();
        if (piece != null) piece.previousSquare = piece.currentSquare;
    }
}