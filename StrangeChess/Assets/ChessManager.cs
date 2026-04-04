using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class ChessManager : MonoBehaviour
{
    [SerializeField] public bool isWhiteTurn = true;
    [SerializeField] public XRSocketInteractor[] sockets = new XRSocketInteractor[64];
    [SerializeField] private XRGrabInteractable[] whitePiecesInteractable;  // 16 pieces
    [SerializeField] private XRGrabInteractable[] blackPiecesInteractable;  // 16 pieces
    [SerializeField] private XRDirectInteractor[] interactors; // 4 interactors, 0 whiteRight, 1 whiteLeft, 2 blackright, 3 blackLeft
    [SerializeField] public SocketTracker lastUnsnap = null;
    [SerializeField] private float pieceSnapTimer = 0.5f;
    [SerializeField] public bool shouldSnapBack = true;
    [SerializeField] private int[] dir = new int[4];

    private void Start()
    {
        foreach(XRDirectInteractor interactor in interactors)
        {
            interactor.selectEntered.AddListener(onGrab);
            interactor.selectExited.AddListener(OnRelease);
        }
    }
    public void ChangeTurn()
    {
        isWhiteTurn = !isWhiteTurn;
        foreach (XRGrabInteractable interactable in whitePiecesInteractable)
        {
            interactable.enabled = isWhiteTurn;

        }
        foreach (XRGrabInteractable interactable in blackPiecesInteractable)
        {
            interactable.enabled = !isWhiteTurn;
        }
        interactors[0].enabled = isWhiteTurn;
        interactors[1].enabled = isWhiteTurn;
        interactors[2].enabled = !isWhiteTurn;
        interactors[3].enabled = !isWhiteTurn;
    }
    public void disableOtherDirectInteractor(XRDirectInteractor correctInteractor)
    {
        foreach (XRDirectInteractor interactor in interactors)
        {
            if (interactor != correctInteractor)
                interactor.enabled = false;
        }
    }
    public void disableOtherDirectInteractor()
    {
        foreach (XRDirectInteractor interactor in interactors)
        {
            interactor.enabled = false;
        }
    }
    public void enableDirectInteractor()
    {
        foreach (XRDirectInteractor interactor in interactors)
        {
            interactor.enabled = true;
        }
    }

    public bool isLegal(XRGrabInteractable grab)
    {

        return false;
    }
    private void onGrab(SelectEnterEventArgs args)
    {
        disableOtherDirectInteractor(args.interactorObject as XRDirectInteractor);

        switch (args.interactableObject.transform.GetComponent<ChessPiece>().piece)
        {
            case PieceType.Pawn:
                pawnMoves(args.interactableObject as XRGrabInteractable);
                break;
            case PieceType.Knight:
                knightMoves();
                break;
            case PieceType.Bishop:
                bishopMoves();
                break;
            case PieceType.Rook:
                rookMoves();
                break;
            case PieceType.Queen:
                queenMoves();
                break;
            case PieceType.King:
                kingMoves();
                break;
        }
        //isLegal(args.interactableObject as XRGrabInteractable);
    }
    void OnRelease(SelectExitEventArgs args)
    {
        Debug.Log("Released: " + args.interactableObject.transform.name);
        StartCoroutine(snapPieceBack(args.interactableObject as XRGrabInteractable));
        // enableAllSockets();
    }
    IEnumerator snapPieceBack(XRGrabInteractable grab)
    {
        disableOtherDirectInteractor();
        yield return new WaitForSeconds(pieceSnapTimer);
        if(shouldSnapBack)
        {
            if(!lastUnsnap.enabled)
                lastUnsnap.enabled = true;
            lastUnsnap.GetComponent<XRSocketInteractor>().StartManualInteraction((IXRSelectInteractable)grab);            
        }
        enableDirectInteractor();
    }

    private void pawnMoves(XRGrabInteractable grab)
    {
        Debug.Log("PawnMoves");

        int currentSquare = lastUnsnap.Square;
        int currentRow = currentSquare / 8;
        int currentFile = currentSquare % 8;

        disableAllSockets(lastUnsnap.GetComponent<XRSocketInteractor>());
        if(currentRow == 1 || currentRow == 6)        
            dir[0] = 2;
        else
            dir[0] = 1;
        dir[1] = 0;
        dir[2] = 0;
        dir[3] = 0;
        straightMoves();
    }
    private void knightMoves()
    {

    }
    private void bishopMoves()
    {
        Debug.Log("BishopMoves");
        disableAllSockets(lastUnsnap.GetComponent<XRSocketInteractor>());
        
        dir[0] = 8;
        dir[1] = 8;
        dir[2] = 8;
        dir[3] = 8;
        
        diagonalMoves();
    }
    private void rookMoves()
    {
        Debug.Log("RookMoves");

        disableAllSockets(lastUnsnap.GetComponent<XRSocketInteractor>());
        dir[0] = 8;
        dir[1] = 8;
        dir[2] = 8;
        dir[3] = 8;
        straightMoves();
    }
    private void queenMoves()
    {
        Debug.Log("QueenMoves");
        disableAllSockets(lastUnsnap.GetComponent<XRSocketInteractor>());
        
        dir[0] = 8; dir[1] = 8; dir[2] = 8; dir[3] = 8;
        
        straightMoves();
        diagonalMoves(); // The Queen uses all 8 directions!
    }
    private void kingMoves()
    {
        Debug.Log("KingMoves");
        disableAllSockets(lastUnsnap.GetComponent<XRSocketInteractor>());
        
        dir[0] = 1; dir[1] = 1; dir[2] = 1; dir[3] = 1;
        
        straightMoves();
        diagonalMoves();
    }
    private void straightMoves()
    {    
        Debug.Log("straightMoves");

        // Get exactly where the piece is right now
        int currentSquare = lastUnsnap.Square;
        
        // Calculate the current file (column) from 0 to 7 to prevent board wrapping
        int currentFile = currentSquare % 8;

        // --- UP (+8) ---
        for (int i = 1; i <= dir[0]; i++)
        {
            Debug.Log("Calculating Up");
            int target = currentSquare + (i * 8);
            
            // 1. Check if we went off the top of the board BEFORE checking the socket
            if (target >= 64) break; 
            // 2. Check if the socket is empty/valid
            if (sockets[target] == null || sockets[target].hasSelection) break;

            sockets[target].GetComponent<BoxCollider>().enabled = true;
        }
        
        // --- DOWN (-8) ---
        for (int i = 1; i <= dir[1]; i++)
        {
            Debug.Log("Calculating Down");
            int target = currentSquare - (i * 8);
            
            // 1. Check if we went off the bottom of the board
            if (target < 0) break;
            if (sockets[target] == null || sockets[target].hasSelection) break;

            sockets[target].GetComponent<BoxCollider>().enabled = true;
        }
        
        // --- RIGHT (+1) ---
        for (int i = 1; i <= dir[2]; i++)
        {
            Debug.Log("Calculating Right");
            int target = currentSquare + i;
            
            // 1. Check if moving right pushes us past the right edge (File 7)
            if (currentFile + i > 7) break; 
            // 2. Standard bounds and socket check
            if (target >= 64 || sockets[target] == null || sockets[target].hasSelection) break;

            sockets[target].GetComponent<BoxCollider>().enabled = true;
        }

        // --- LEFT (-1) ---
        for (int i = 1; i <= dir[3]; i++)
        {
            Debug.Log("Calculating Left");
            int target = currentSquare - i;
            
            // 1. Check if moving left pushes us past the left edge (File 0)
            if (currentFile - i < 0) break;
            // 2. Standard bounds and socket check
            if (target < 0 || sockets[target] == null || sockets[target].hasSelection) break;

            sockets[target].GetComponent<BoxCollider>().enabled = true;
        }
    }

    private void diagonalMoves()
    {    
        Debug.Log("diagonalMoves");

        int currentSquare = lastUnsnap.Square;
        int currentFile = currentSquare % 8;

        // --- UP-LEFT (+7) | dir[0] ---
        for (int i = 1; i <= dir[0]; i++)
        {
            Debug.Log("Calculating Up-Left");
            int target = currentSquare + (i * 7);
            
            // 1. Edge Check: Moving left means the file decreases. Stop if it drops below 0 (A-file).
            if (currentFile - i < 0) break; 
            
            // 2. Bounds Check: Stop if we go off the top of the board.
            if (target >= 64) break; 
            
            // 3. Occupied Check
            if (sockets[target] == null || sockets[target].hasSelection) break;

            sockets[target].GetComponent<BoxCollider>().enabled = true;
        }
        
        // --- UP-RIGHT (+9) | dir[1] ---
        for (int i = 1; i <= dir[1]; i++)
        {
            Debug.Log("Calculating Up-Right");
            int target = currentSquare + (i * 9);
            
            // 1. Edge Check: Moving right means the file increases. Stop if it passes 7 (H-file).
            if (currentFile + i > 7) break; 
            
            if (target >= 64) break;
            if (sockets[target] == null || sockets[target].hasSelection) break;

            sockets[target].GetComponent<BoxCollider>().enabled = true;
        }
        
        // --- DOWN-LEFT (-9) | dir[2] ---
        for (int i = 1; i <= dir[2]; i++)
        {
            Debug.Log("Calculating Down-Left");
            int target = currentSquare - (i * 9);
            
            // 1. Edge Check: Moving left, stop if file drops below 0.
            if (currentFile - i < 0) break;
            
            // 2. Bounds Check: Stop if we go off the bottom of the board.
            if (target < 0) break;
            
            if (sockets[target] == null || sockets[target].hasSelection) break;

            sockets[target].GetComponent<BoxCollider>().enabled = true;
        }

        // --- DOWN-RIGHT (-7) | dir[3] ---
        for (int i = 1; i <= dir[3]; i++)
        {
            Debug.Log("Calculating Down-Right");
            int target = currentSquare - (i * 7);
            
            // 1. Edge Check: Moving right, stop if file passes 7.
            if (currentFile + i > 7) break;
            
            if (target < 0) break;
            if (sockets[target] == null || sockets[target].hasSelection) break;

            sockets[target].GetComponent<BoxCollider>().enabled = true;
        }
    }    
    private void disableAllSockets(XRSocketInteractor lastUnsnapSocket)
    {
        Debug.Log("DisableAllSockets");
        foreach(XRSocketInteractor socket in sockets)
        {
            if(lastUnsnapSocket == socket)
                socket.GetComponent<BoxCollider>().enabled = true;
            else
                socket.GetComponent<BoxCollider>().enabled = false;
        }
    }
    public void enableAllSockets()
    {
        Debug.Log("DisableAllSockets");
        foreach(XRSocketInteractor socket in sockets)
        {
                socket.GetComponent<BoxCollider>().enabled = true;
        }
    }
}