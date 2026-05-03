using UnityEngine;

public class ClickDetector : MonoBehaviour
{
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
                if (hit.collider.GetComponent<SocketTracker>() != null) // detecting Square NOT Piece
                {
                    //  correct index of which SQUARE was clicked
                    int correctIndex = ToBitboardIndex(hit.collider.GetComponent<SocketTracker>().Square);

                    //  index of which Square clicked on, in BITBOARD
                    ulong from = 1UL << correctIndex;
                    
                    Debug.Log(correctIndex + " - " + Board.Instance.bitboardToPiece(from) + " - " + Board.Instance.displayBitboard(from));
                    Chess.Instance.pawnMoves(from);

                    if(isSelected && (from & Board.Instance.whitePieces) == 0)
                    {
                        if((from & availableMoves) != 0)    // selected piece can move at from
                        {
                            Chess.Instance.movePiece(selectedPiece, from);
                            availableMoves = 0;
                            isSelected = false;
                        }
                    }
                    else if((from & Board.Instance.whitePieces) != 0)
                    {
                        Debug.Log("Selected");
                        isSelected = true;
                        selectedPiece = from;
                    }
                }
            }
        }
    }

    public int ToBitboardIndex(int i)
    {
        int file = i % 8;
        int rank = i / 8;

        return (7 - rank) * 8 + file;
    }
    
}