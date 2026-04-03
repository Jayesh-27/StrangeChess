using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public enum squares
{
    A1, A2, A3, A4, A5, A6, A7, A8,
    B1, B2, B3, B4, B5, B6, B7, B8,
    C1, C2, C3, C4, C5, C6, C7, C8,
    D1, D2, D3, D4, D5, D6, D7, D8,
    E1, E2, E3, E4, E5, E6, E7, E8,
    F1, F2, F3, F4, F5, F6, F7, F8,
    G1, G2, G3, G4, G5, G6, G7, G8,
    H1, H2, H3, H4, H5, H6, H7, H8,
}
public class SocketTracker : MonoBehaviour
{
    public XRSocketInteractor socket;
    //public squares square;
    public int Square = 0;
    [SerializeField] ChessManager chessManager;

    void Start()
    {
        socket = GetComponent<XRSocketInteractor>();

        socket.selectEntered.AddListener(OnSnap);
        socket.selectExited.AddListener(OnUnsnap);

        if (socket.hasSelection)
        {
            Debug.Log("Something is already in socket");
        }
    }

    void OnSnap(SelectEnterEventArgs args)
    {
        Debug.Log(args.interactableObject.transform.name + " Snapped");
        chessManager.shouldSnapBack = false;
    }

    void OnUnsnap(SelectExitEventArgs args)
    {
        chessManager.shouldSnapBack = true;
        chessManager.lastUnsnap = this;

        // if unsnapped disable all the other direct interactors including other teams
        //chessManager.disableOtherDirectInteractor(args.interactorObject as XRDirectInteractor);

        //chessManager.enableAllDirectInteractor(args.interactorObject as XRDirectInteractor);
        Debug.Log(args.interactableObject.transform.name + " Removed");
    }
}