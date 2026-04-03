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
    }
    IEnumerator snapPieceBack(XRGrabInteractable grab)
    {
        yield return new WaitForSeconds(pieceSnapTimer);
        if(shouldSnapBack)
        {
            if(!lastUnsnap.enabled)
                lastUnsnap.enabled = true;
            lastUnsnap.GetComponent<XRSocketInteractor>().StartManualInteraction((IXRSelectInteractable)grab);
            enableDirectInteractor();
        }
    }

    private void pawnMoves(XRGrabInteractable grab)
    {
        Debug.Log("PawnMoves");
        //  grab = interactable/piece
        //  lastUnsnap = socket
        disableAllSockets(lastUnsnap.GetComponent<XRSocketInteractor>());
        verticalMoves();
    }
    private void knightMoves()
    {

    }
    private void bishopMoves()
    {

    }
    private void rookMoves()
    {

    }
    private void queenMoves()
    {

    }
    private void kingMoves()
    {

    }
    private void verticalMoves()
    {
        Debug.Log("verticalMoves");
        for (int i = 0; i < 64; i += 8)
        {
            if (sockets[lastUnsnap.Square + i].hasSelection)
                break;

            sockets[lastUnsnap.Square + i].enabled = true;
        }
    }
    private void disableAllSockets(XRSocketInteractor lastUnsnapSocket)
    {
        Debug.Log("DisableAllSockets");
        foreach(XRSocketInteractor socket in sockets)
        {
            if(lastUnsnapSocket == socket)
                //socket.enabled = true;
            else
                //socket.enabled = false;
        }
    }
}