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
            Debug.Log("Left Click Pressed");
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                Debug.Log("Raycast nikal gayi");
                if (hit.collider.GetComponent<SocketTracker>() != null) // detecting Square; NOT Piece
                {
                    Debug.Log("collided with SocketTracker");
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
                                // --- FIX: Added 'selectedPiece == (1UL << 4)' to ensure King started on e1 ---
                                if(selectedPiece == (1UL << 4) && Board.Instance.bitboardToPiece(selectedPiece) == pieceType.whiteKing && (from & (1UL << 6)) != 0)
                                {
                                    Board.Instance.Move3DModel(1UL << 7, 1UL << 5);
                                }
                                else if(selectedPiece == (1UL << 4) && Board.Instance.bitboardToPiece(selectedPiece) == pieceType.whiteKing && (from & (1UL << 2)) != 0)
                                {
                                    Board.Instance.Move3DModel(1UL << 0, 1UL << 3);
                                }
                                int fromIndex = Board.Instance.GetBitboardIndex(selectedPiece);
                                int toIndex = Board.Instance.GetBitboardIndex(from);
                                pieceType movingPiece = Board.Instance.bitboardToPiece(selectedPiece);
                                pieceType targetPiece = Board.Instance.bitboardToPiece(from);

                                // --- VISUAL ROUTING ---
                                if (movingPiece == pieceType.whitePawn && toIndex >= 56)
                                {
                                    Board.Instance.PromoteVisualPiece(fromIndex, toIndex, true);
                                }
                                else if (movingPiece == pieceType.blackPawn && toIndex <= 7)
                                {
                                    Board.Instance.PromoteVisualPiece(fromIndex, toIndex, false);
                                }
                                else 
                                {
                                    // Normal mesh teleporting for standard moves
                                    Board.Instance.Move3DModel(selectedPiece, from);
                                }

                                // En Passant Visual Destruction 
                                if (movingPiece == pieceType.whitePawn && targetPiece == pieceType.none && (toIndex == fromIndex + 7 || toIndex == fromIndex + 9)) Board.Instance.DestroyVisualPiece(toIndex - 8);
                                else if (movingPiece == pieceType.blackPawn && targetPiece == pieceType.none && (toIndex == fromIndex - 7 || toIndex == fromIndex - 9)) Board.Instance.DestroyVisualPiece(toIndex + 8);

                                if (StockfishTester.Instance != null) StockfishTester.Instance.ReportUserMove(selectedPiece, from, movingPiece);
                                
                                Chess.Instance.movePiece(selectedPiece, from);

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
                                // --- FIX: Added 'selectedPiece == (1UL << 60)' to ensure King started on e8 ---
                                if(selectedPiece == (1UL << 60) && Board.Instance.bitboardToPiece(selectedPiece) == pieceType.blackKing && (from & (1UL << 62)) != 0)
                                {
                                    Board.Instance.Move3DModel(1UL << 63, 1UL << 61);
                                }
                                else if(selectedPiece == (1UL << 60) && Board.Instance.bitboardToPiece(selectedPiece) == pieceType.blackKing && (from & (1UL << 58)) != 0)
                                {
                                    Board.Instance.Move3DModel(1UL << 56, 1UL << 59);
                                }
                                int fromIndex = Board.Instance.GetBitboardIndex(selectedPiece);
                                int toIndex = Board.Instance.GetBitboardIndex(from);
                                pieceType movingPiece = Board.Instance.bitboardToPiece(selectedPiece);
                                pieceType targetPiece = Board.Instance.bitboardToPiece(from);

                                // --- VISUAL ROUTING ---
                                if (movingPiece == pieceType.whitePawn && toIndex >= 56)
                                {
                                    Board.Instance.PromoteVisualPiece(fromIndex, toIndex, true);

                                }
                                else if (movingPiece == pieceType.blackPawn && toIndex <= 7)
                                {
                                    Board.Instance.PromoteVisualPiece(fromIndex, toIndex, false);
                                }
                                else 
                                {
                                    // Normal mesh teleporting for standard moves
                                    Board.Instance.Move3DModel(selectedPiece, from);
                                }

                                // En Passant Visual Destruction 
                                if (movingPiece == pieceType.whitePawn && targetPiece == pieceType.none && (toIndex == fromIndex + 7 || toIndex == fromIndex + 9)) Board.Instance.DestroyVisualPiece(toIndex - 8);
                                else if (movingPiece == pieceType.blackPawn && targetPiece == pieceType.none && (toIndex == fromIndex - 7 || toIndex == fromIndex - 9)) Board.Instance.DestroyVisualPiece(toIndex + 8);

                                if (StockfishTester.Instance != null) StockfishTester.Instance.ReportUserMove(selectedPiece, from, movingPiece);

                                Chess.Instance.movePiece(selectedPiece, from);

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
                            availableMoves = Chess.Instance.pawnMoves(from);
                            availableMoves = Chess.Instance.checkLegalMoves(from, availableMoves);
                        }
                        else if(Board.Instance.bitboardToPiece(from) == pieceType.whiteKnight)
                        {
                            availableMoves = Chess.Instance.knightMoves(from);
                            availableMoves = Chess.Instance.checkLegalMoves(from, availableMoves);
                        }
                        else if(Board.Instance.bitboardToPiece(from) == pieceType.whiteKing)
                        {
                            availableMoves = Chess.Instance.kingMoves(from);      
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
                            availableMoves = Chess.Instance.pawnMoves(from);
                            availableMoves = Chess.Instance.checkLegalMoves(from, availableMoves);
                        }
                        else if(Board.Instance.bitboardToPiece(from) == pieceType.blackKnight)
                        {
                            availableMoves = Chess.Instance.knightMoves(from);
                            availableMoves = Chess.Instance.checkLegalMoves(from, availableMoves);
                        }
                        else if(Board.Instance.bitboardToPiece(from) == pieceType.blackKing)
                        {
                            availableMoves = Chess.Instance.kingMoves(from);       
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