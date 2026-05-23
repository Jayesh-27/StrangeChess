using UnityEngine;

public class ClickDetector : MonoBehaviour
{
    public bool isWhiteTurn = true;
    public static ClickDetector Instance;
    public bool isSelected = false;
    private ulong selectedPiece = 0;
    public ulong availableMoves = 0;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                if (hit.collider.GetComponent<SocketTracker>() != null) // detecting Square; NOT Piece
                {
                    int Index = hit.collider.GetComponent<SocketTracker>().Square;
                    //Debug.Log(Board.Instance.BitboardToBoardString(Board.Instance.rookBlockersMasks[Index]));
                    //  index of which Square clicked on, in BITBOARD
                    ulong from = 1UL << Index;
                    Debug.Log(Index + " - " + Board.Instance.bitboardToPiece(from) + " - " + Board.Instance.displayBitboard(from));
                    
                    if(isSelected)
                    {
                        if(isWhiteTurn && (from & Board.Instance.whitePieces) == 0)
                        {
                            if((from & availableMoves) != 0)    // selected piece can move at from
                            {
                                //Debug.Log("Moved");
                                Chess.Instance.movePiece(selectedPiece, from);
                                availableMoves = 0;
                            }
                            isSelected = false;
                            //Debug.Log("unselected");                            
                        }
                        else if(!isWhiteTurn && (from & Board.Instance.blackPieces) == 0)
                        {
                            if((from & availableMoves) != 0)    // selected piece can move at from
                            {
                                Chess.Instance.movePiece(selectedPiece, from);
                                availableMoves = 0;
                            }
                            isSelected = false;
                            //Debug.Log("unselected");                            
                        }
                    }
                    if(isWhiteTurn && (from & Board.Instance.whitePieces) != 0)
                    {
                        availableMoves = 0;
                        if(Board.Instance.bitboardToPiece(from) == pieceType.whitePawn)
                        {
                            Chess.Instance.pawnMoves(from);
                        }
                        else if(Board.Instance.bitboardToPiece(from) == pieceType.whiteKnight)
                        {
                            Chess.Instance.knightMoves(from);
                        }
                        else if(Board.Instance.bitboardToPiece(from) == pieceType.whiteKing)
                        {
                            Chess.Instance.kingMoves(from);                            
                        }
                        else if(Board.Instance.bitboardToPiece(from) == pieceType.whiteBishop)
                        {
                            Chess.Instance.bishopMoves(from);
                        }
                        else if(Board.Instance.bitboardToPiece(from) == pieceType.whiteRook)
                        {
                            Chess.Instance.rookMoves(from);
                        }
                        else if(Board.Instance.bitboardToPiece(from) == pieceType.whiteQueen)
                        {
                            Chess.Instance.queenMoves(from);
                        }
                        //Debug.Log("Selected");
                        isSelected = true;
                        selectedPiece = from;
                    }
                    if(!isWhiteTurn && (from & Board.Instance.blackPieces) != 0)
                    {
                        availableMoves = 0;
                        if(Board.Instance.bitboardToPiece(from) == pieceType.blackPawn)
                        {
                            Chess.Instance.pawnMoves(from);
                        }
                        else if(Board.Instance.bitboardToPiece(from) == pieceType.blackKnight)
                        {
                            Chess.Instance.knightMoves(from);
                        }
                        else if(Board.Instance.bitboardToPiece(from) == pieceType.blackKing)
                        {
                            Chess.Instance.kingMoves(from);                            
                        }
                        else if(Board.Instance.bitboardToPiece(from) == pieceType.blackBishop)
                        {
                            Chess.Instance.bishopMoves(from);
                        }
                        else if(Board.Instance.bitboardToPiece(from) == pieceType.blackRook)
                        {
                            Chess.Instance.rookMoves(from);
                        }
                        else if(Board.Instance.bitboardToPiece(from) == pieceType.blackQueen)
                        {
                            Chess.Instance.queenMoves(from);
                        }
                        Debug.Log("Selected");
                        isSelected = true;
                        selectedPiece = from;
                    }
                    Debug.Log(Board.Instance.BitboardToBoardString(Board.Instance.allPieces));
                }
            }
        }
    }    
}