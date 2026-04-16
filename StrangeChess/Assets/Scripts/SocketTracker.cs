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
                    
                    // Call the instant capture function
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
        
        ChessPiece piece = args.interactableObject.transform.GetComponent<ChessPiece>();
        if (piece != null) piece.previousSquare = piece.currentSquare;
    }
}