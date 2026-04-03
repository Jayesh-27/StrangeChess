using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public enum PieceType
{
    Pawn,
    Knight,
    Bishop,
    Rook,
    Queen,
    King
}


public class ChessPiece : MonoBehaviour
{
    public bool isWhitePiece = true;
    public PieceType piece;
    public XRGrabInteractable grabInteractable;

    void Start()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        grabInteractable.selectEntered.AddListener(_ => onGrab());
        grabInteractable.selectExited.AddListener(_ => Debug.Log(name + " released"));
    }
    private void onGrab()
    {
        //chessManager.isW
    }
    private void PawnMoveCalculate()
    {

    }
}