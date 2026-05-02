using UnityEngine;

public class ClickDetector : MonoBehaviour
{
    public static ClickDetector clickDetector;
    public bool isSelected = false;
    private ulong selectedPiece = 0;
    public ulong availableMoves = 0;

    void Awake()
    {
        if (clickDetector == null)
        {
            clickDetector = this;
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
                if (hit.collider.GetComponent<SocketTracker>() != null) // detecting Square NOT Piece
                {
                    //  correct index of which SQUARE was clicked
                    int correctIndex = ToBitboardIndex(hit.collider.GetComponent<SocketTracker>().Square);

                    //  index of which Square clicked on, in BITBOARD
                    ulong from = 1UL << correctIndex;
                    string selectedPieceString = bitboardToPiece(from);
                    
                    Debug.Log(correctIndex + " - " + selectedPieceString + " - " + Board.board.displayBitboard(from));
                    
                    if(isSelected && (from & Board.board.whitePieces) == 0)
                    {
                        if((from & availableMoves) == 0)    // selected piece can move at from
                        {
                            Chess.chess.movePiece(selectedPiece, from);
                            isSelected = false;
                        }
                    }
                    else if((from & Board.board.allPieces) != 0)
                    {
                        Debug.Log("Selected");
                        isSelected = true;
                        selectedPiece = from;
                    }
                }
            }
        }
    }

    int ToBitboardIndex(int i)
    {
        int file = i % 8;
        int rank = i / 8;

        return (7 - rank) * 8 + file;
    }
    
    string bitboardToPiece(ulong from)
    {
        if((from & Board.board.allPieces) != 0)
        {
            if ((from & Board.board.whitePieces) != 0)
            {
                if ((from & Board.board.whitePawn) != 0)
                {
                    Chess.chess.pawnMoves(from);
                    return "whitePawn";
                }
                else if ((from & Board.board.whiteKnight) != 0)
                {
                    return "whiteKnight";
                }
                else if ((from & Board.board.whiteBishop) != 0)
                {
                    return "whiteBishop";
                }
                else if ((from & Board.board.whiteRook) != 0)
                {
                    return "whiteRook";
                }
                else if ((from & Board.board.whiteQueen) != 0)
                {
                    return "whiteQueen";
                }
                else if ((from & Board.board.whiteKing) != 0)
                {
                    return "whiteKing";
                }
            }
            else
            {
                if ((from & Board.board.blackPawn) != 0)
                {
                    return "blackPawn";
                }
                else if ((from & Board.board.blackKnight) != 0)
                {
                    return "blackKnight";
                }
                else if ((from & Board.board.blackBishop) != 0)
                {
                    return "blackBishop";
                }
                else if ((from & Board.board.blackRook) != 0)
                {
                    return "blackRook";
                }
                else if ((from & Board.board.blackQueen) != 0)
                {
                    return "blackQueen";
                }
                else if ((from & Board.board.blackKing) != 0)
                {
                    return "blackKing";
                }
            }
        }
        else
        {
            return "No Piece";
        }
        return "Unknown piece";
    }
}