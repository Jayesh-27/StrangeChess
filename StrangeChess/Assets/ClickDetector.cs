using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

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
                                if(Board.Instance.bitboardToPiece(selectedPiece) == pieceType.whiteKing && (from & (1UL << 6)) != 0)
                                {
                                    Board.Instance.Move3DModel(1UL << 7, 1UL << 5);
                                }
                                else if(Board.Instance.bitboardToPiece(selectedPiece) == pieceType.whiteKing && (from & (1UL << 2)) != 0)
                                {
                                    Board.Instance.Move3DModel(1UL << 0, 1UL << 3);
                                }
                                Chess.Instance.movePiece(selectedPiece, from);
                                Board.Instance.Move3DModel(selectedPiece, from);

                                ClickDetector.Instance.isWhiteTurn = !ClickDetector.Instance.isWhiteTurn;
                                availableMoves = 0;
                            }
                            isSelected = false;
                            //Debug.Log("unselected");                            
                        }
                        else if(!isWhiteTurn && (from & Board.Instance.blackPieces) == 0)
                        {
                            if((from & availableMoves) != 0)    // selected piece can move at from
                            {
                                if(Board.Instance.bitboardToPiece(selectedPiece) == pieceType.blackKing && (from & (1UL << 62)) != 0)
                                {
                                    Board.Instance.Move3DModel(1UL << 63, 1UL << 61);
                                }
                                else if(Board.Instance.bitboardToPiece(selectedPiece) == pieceType.blackKing && (from & (1UL << 58)) != 0)
                                {
                                    Board.Instance.Move3DModel(1UL << 56, 1UL << 59);
                                }
                                Chess.Instance.movePiece(selectedPiece, from);
                                Board.Instance.Move3DModel(selectedPiece, from);

                                ClickDetector.Instance.isWhiteTurn = !ClickDetector.Instance.isWhiteTurn;
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
                            availableMoves = Chess.Instance.checkLegalMoves(from, availableMoves);
                        }
                        else if(Board.Instance.bitboardToPiece(from) == pieceType.whiteKnight)
                        {
                            availableMoves = Chess.Instance.knightMoves(from);
                            availableMoves = Chess.Instance.checkLegalMoves(from, availableMoves);
                        }
                        else if(Board.Instance.bitboardToPiece(from) == pieceType.whiteKing)
                        {
                            Chess.Instance.kingMoves(from);      
                            availableMoves = Chess.Instance.checkLegalMoves(from, availableMoves);
                        }
                        else if(Board.Instance.bitboardToPiece(from) == pieceType.whiteBishop)
                        {
                            availableMoves = Chess.Instance.bishopMoves(from);
                            availableMoves = Chess.Instance.checkLegalMoves(from, availableMoves);
                        }
                        else if(Board.Instance.bitboardToPiece(from) == pieceType.whiteRook)
                        {
                            availableMoves = Chess.Instance.rookMoves(from);
                            availableMoves = Chess.Instance.checkLegalMoves(from, availableMoves);
                        }
                        else if(Board.Instance.bitboardToPiece(from) == pieceType.whiteQueen)
                        {
                            availableMoves = Chess.Instance.queenMoves(from);
                            availableMoves = Chess.Instance.checkLegalMoves(from, availableMoves);
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
                            availableMoves = Chess.Instance.checkLegalMoves(from, availableMoves);
                        }
                        else if(Board.Instance.bitboardToPiece(from) == pieceType.blackKnight)
                        {
                            availableMoves = Chess.Instance.knightMoves(from);
                            availableMoves = Chess.Instance.checkLegalMoves(from, availableMoves);
                        }
                        else if(Board.Instance.bitboardToPiece(from) == pieceType.blackKing)
                        {
                            Chess.Instance.kingMoves(from);       
                            availableMoves = Chess.Instance.checkLegalMoves(from, availableMoves);                     
                        }
                        else if(Board.Instance.bitboardToPiece(from) == pieceType.blackBishop)
                        {
                            availableMoves = Chess.Instance.bishopMoves(from);
                            availableMoves = Chess.Instance.checkLegalMoves(from, availableMoves);
                        }
                        else if(Board.Instance.bitboardToPiece(from) == pieceType.blackRook)
                        {
                            availableMoves = Chess.Instance.rookMoves(from);
                            availableMoves = Chess.Instance.checkLegalMoves(from, availableMoves);
                        }
                        else if(Board.Instance.bitboardToPiece(from) == pieceType.blackQueen)
                        {
                            availableMoves = Chess.Instance.queenMoves(from);
                            availableMoves = Chess.Instance.checkLegalMoves(from, availableMoves);
                        }
                        Debug.Log("Selected");
                        isSelected = true;
                        selectedPiece = from;
                    }
                    Debug.Log("AVAILABLE MOVES \n\n" + Board.Instance.BitboardToBoardString(availableMoves));
                }
            }
        }
    }    
}