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
                    int Index = hit.collider.GetComponent<SocketTracker>().Square;

                    //  index of which Square clicked on, in BITBOARD
                    ulong from = 1UL << Index;
                    
                    Debug.Log(Index + " - " + Board.Instance.bitboardToPiece(from) + " - " + Board.Instance.displayBitboard(from));
                    
                    if(isSelected && (from & Board.Instance.whitePieces) == 0)
                    {
                        if((from & availableMoves) != 0)    // selected piece can move at from
                        {
                            Chess.Instance.movePiece(selectedPiece, from);
                            availableMoves = 0;
                        }
                    }
                    if(isSelected)
                        isSelected = !isSelected;
                    if((from & Board.Instance.whitePieces) != 0)
                    {
                        if(Board.Instance.bitboardToPiece(from) == pieceType.whitePawn)
                        {
                            Chess.Instance.pawnMoves(from);
                            Debug.Log("Selected");
                            isSelected = true;
                            selectedPiece = from;
                        }
                    }
                }
            }
        }
    }    
}