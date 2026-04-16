using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using Unity.XR.CoreUtils;

public class ChessManager : NetworkBehaviour
{
    [Header("Game State")]
    public NetworkVariable<bool> isWhiteTurn = new NetworkVariable<bool>(true);
    [SerializeField] private bool changetheTurn = false;
    [SerializeField] public bool shouldSnapBack = true;
    [SerializeField] private float pieceSnapTimer = 0.5f;

    [Header("Board References")]
    [SerializeField] public XRSocketInteractor[] sockets = new XRSocketInteractor[64];
    [SerializeField] private XRGrabInteractable[] whitePiecesInteractable;  
    [SerializeField] private XRGrabInteractable[] blackPiecesInteractable;  
    [SerializeField] private XRDirectInteractor[] interactors; 

    [Header("Spawn Points")]
    [SerializeField] private GameObject XRRig;
    [SerializeField] private GameObject WhiteRigSpawnPoint;
    [SerializeField] private GameObject BlackRigSpawnPoint;

    private int[] dir = new int[4];

    private void Awake()
    {
        foreach (XRDirectInteractor interactor in interactors)
        {
            interactor.selectEntered.AddListener(onGrab);
            interactor.selectExited.AddListener(OnRelease);
        }
    }

    public override void OnNetworkSpawn()
    {
        XROrigin xrOrigin = XRRig.GetComponent<XROrigin>();

        isWhiteTurn.OnValueChanged += (bool prev, bool current) => {
            UpdateHandPermissions(); 
        };

        if (IsHost)
        {
            TeleportVRPlayer(xrOrigin, WhiteRigSpawnPoint.transform);
            
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client.ClientId != NetworkManager.Singleton.LocalClientId) GrantBlackPieceOwnership(client.ClientId);
            }
            NetworkManager.Singleton.OnClientConnectedCallback += GrantBlackPieceOwnership;
        }
        else
        {
            TeleportVRPlayer(xrOrigin, BlackRigSpawnPoint.transform);
        }

        UpdateHandPermissions();
    }

    private void TeleportVRPlayer(XROrigin rig, Transform target)
    {
        rig.transform.position = target.position;
        rig.transform.rotation = target.rotation;
        rig.MoveCameraToWorldLocation(target.position);
        rig.MatchOriginUpCameraForward(target.up, target.forward);
    }

    private void GrantBlackPieceOwnership(ulong clientId)
    {
        if (clientId != NetworkManager.Singleton.LocalClientId)
        {
            foreach (XRGrabInteractable interactable in blackPiecesInteractable)
            {
                NetworkObject netObj = interactable.GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsSpawned) netObj.ChangeOwnership(clientId);
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton != null && IsHost)
            NetworkManager.Singleton.OnClientConnectedCallback -= GrantBlackPieceOwnership;
    }

    private void Update()
    {
        if (changetheTurn) { changetheTurn = false; RequestChangeTurn(); }
    }

    public void UpdateHandPermissions()
    {
        string myTeamLayer = IsHost ? "WhitePieces" : "BlackPieces";
        bool isMyTurn = (IsHost && isWhiteTurn.Value) || (!IsHost && !isWhiteTurn.Value);

        foreach (XRDirectInteractor interactor in interactors)
        {
            if (isMyTurn) interactor.interactionLayers = InteractionLayerMask.GetMask(myTeamLayer);
            else interactor.interactionLayers = 0; 
        }
    }

    public void disableOtherDirectInteractor(XRDirectInteractor correctInteractor)
    {
        foreach (XRDirectInteractor interactor in interactors) if (interactor != correctInteractor) interactor.enabled = false;
    }

    public void disableOtherDirectInteractor()
    {
        foreach (XRDirectInteractor interactor in interactors) interactor.enabled = false;
    }

    public void enableDirectInteractor()
    {
        foreach (XRDirectInteractor interactor in interactors) interactor.enabled = true;
    }

    private void onGrab(SelectEnterEventArgs args)
    {
        ChessPiece piece = args.interactableObject.transform.GetComponent<ChessPiece>();
        NetworkObject netObj = args.interactableObject.transform.GetComponent<NetworkObject>();
        
        if (netObj != null) ForceUnsnapServerRpc(netObj.NetworkObjectId);
        disableOtherDirectInteractor(args.interactorObject as XRDirectInteractor);

        switch (piece.piece)
        {
            case PieceType.Pawn: pawnMoves(piece); break;
            case PieceType.Knight: knightMoves(piece); break;
            case PieceType.Bishop: bishopMoves(piece); break;
            case PieceType.Rook: rookMoves(piece); break;
            case PieceType.Queen: queenMoves(piece); break;
            case PieceType.King: kingMoves(piece); break;
        }
    }

    void OnRelease(SelectExitEventArgs args)
    {
        StartCoroutine(snapPieceBack(args.interactableObject as XRGrabInteractable));
    }

    IEnumerator snapPieceBack(XRGrabInteractable grab)
    {
        disableOtherDirectInteractor();
        yield return new WaitForSeconds(pieceSnapTimer);
        
        ChessPiece piece = grab.GetComponent<ChessPiece>();
        IXRSelectInteractor currentInteractor = grab.interactorsSelecting.Count > 0 ? grab.interactorsSelecting[0] : null;
        XRSocketInteractor currentSocket = currentInteractor as XRSocketInteractor;

        if (shouldSnapBack && piece.previousSquare != -1)
        {
            XRSocketInteractor originalSocket = sockets[piece.previousSquare];
            if (!originalSocket.enabled) originalSocket.enabled = true;
            originalSocket.StartManualInteraction((IXRSelectInteractable)grab);
            piece.currentSquare = piece.previousSquare; 
        }
        else if (currentSocket != null && piece.currentSquare != piece.previousSquare)
        {
            RequestChangeTurn();
            piece.previousSquare = piece.currentSquare; 
        }
        
        enableDirectInteractor();
    }

    private bool CheckAndEnableSocket(int targetSquare, ChessPiece attackingPiece)
    {
        if (targetSquare < 0 || targetSquare >= 64 || sockets[targetSquare] == null) return false;

        XRSocketInteractor socket = sockets[targetSquare];

        if (socket.hasSelection)
        {
            ChessPiece defendingPiece = socket.firstInteractableSelected.transform.GetComponent<ChessPiece>();
            if (defendingPiece != null && defendingPiece.isWhitePiece != attackingPiece.isWhitePiece)
                socket.GetComponent<BoxCollider>().enabled = true;
            
            return false; 
        }

        socket.GetComponent<BoxCollider>().enabled = true;
        return true;
    }

    private void disableAllSockets(int originalSquare)
    {
        for(int i = 0; i < 64; i++) sockets[i].GetComponent<BoxCollider>().enabled = (i == originalSquare);
    }

    public void enableAllSockets()
    {
        foreach (XRSocketInteractor socket in sockets) socket.GetComponent<BoxCollider>().enabled = true;
    }

    // --- PIECE MOVEMENT LOGIC ---
    private void pawnMoves(ChessPiece piece)
    {
        int currentSquare = piece.currentSquare;
        int currentRow = currentSquare / 8;
        int currentFile = currentSquare % 8;
        disableAllSockets(currentSquare);

        int forwardStep = piece.isWhitePiece ? 8 : -8;
        int startRow = piece.isWhitePiece ? 1 : 6;

        int target1 = currentSquare + forwardStep;
        if (target1 >= 0 && target1 < 64 && !sockets[target1].hasSelection)
        {
            sockets[target1].GetComponent<BoxCollider>().enabled = true;
            if (currentRow == startRow)
            {
                int target2 = currentSquare + (forwardStep * 2);
                if (target2 >= 0 && target2 < 64 && !sockets[target2].hasSelection)
                    sockets[target2].GetComponent<BoxCollider>().enabled = true;
            }
        }

        if (currentFile > 0)
        {
            int capLeft = currentSquare + forwardStep - 1;
            if (capLeft >= 0 && capLeft < 64 && sockets[capLeft].hasSelection)
            {
                ChessPiece def = sockets[capLeft].firstInteractableSelected.transform.GetComponent<ChessPiece>();
                if (def != null && def.isWhitePiece != piece.isWhitePiece) sockets[capLeft].GetComponent<BoxCollider>().enabled = true;
            }
        }

        if (currentFile < 7)
        {
            int capRight = currentSquare + forwardStep + 1;
            if (capRight >= 0 && capRight < 64 && sockets[capRight].hasSelection)
            {
                ChessPiece def = sockets[capRight].firstInteractableSelected.transform.GetComponent<ChessPiece>();
                if (def != null && def.isWhitePiece != piece.isWhitePiece) sockets[capRight].GetComponent<BoxCollider>().enabled = true;
            }
        }
    }

    private void knightMoves(ChessPiece piece)
    {
        disableAllSockets(piece.currentSquare);
        int currentSquare = piece.currentSquare;
        int currentFile = currentSquare % 8;
        int currentRow = currentSquare / 8;
        int[] knightOffsets = { 15, 17, 10, -6, -15, -17, -10, 6 };

        foreach (int offset in knightOffsets)
        {
            int target = currentSquare + offset;
            if (target >= 0 && target < 64)
            {
                int fileDiff = Mathf.Abs(currentFile - (target % 8));
                int rowDiff = Mathf.Abs(currentRow - (target / 8));
                if ((fileDiff == 1 && rowDiff == 2) || (fileDiff == 2 && rowDiff == 1)) 
                    CheckAndEnableSocket(target, piece);
            }
        }
    }

    private void bishopMoves(ChessPiece piece)
    {
        disableAllSockets(piece.currentSquare);
        dir[0] = 8; dir[1] = 8; dir[2] = 8; dir[3] = 8;
        diagonalMoves(piece);
    }

    private void rookMoves(ChessPiece piece)
    {
        disableAllSockets(piece.currentSquare);
        dir[0] = 8; dir[1] = 8; dir[2] = 8; dir[3] = 8;
        straightMoves(piece);
    }

    private void queenMoves(ChessPiece piece)
    {
        disableAllSockets(piece.currentSquare);
        dir[0] = 8; dir[1] = 8; dir[2] = 8; dir[3] = 8;
        straightMoves(piece);
        diagonalMoves(piece);
    }

    private void kingMoves(ChessPiece piece)
    {
        disableAllSockets(piece.currentSquare);
        dir[0] = 1; dir[1] = 1; dir[2] = 1; dir[3] = 1;
        straightMoves(piece);
        diagonalMoves(piece);
    }

    private void straightMoves(ChessPiece piece)
    {
        int currentSquare = piece.currentSquare;
        int currentFile = currentSquare % 8;

        for (int i = 1; i <= dir[0]; i++) if (!CheckAndEnableSocket(currentSquare + (i * 8), piece)) break;
        for (int i = 1; i <= dir[1]; i++) if (!CheckAndEnableSocket(currentSquare - (i * 8), piece)) break;
        for (int i = 1; i <= dir[2]; i++) { if (currentFile + i > 7) break; if (!CheckAndEnableSocket(currentSquare + i, piece)) break; }
        for (int i = 1; i <= dir[3]; i++) { if (currentFile - i < 0) break; if (!CheckAndEnableSocket(currentSquare - i, piece)) break; }
    }

    private void diagonalMoves(ChessPiece piece)
    {
        int currentSquare = piece.currentSquare;
        int currentFile = currentSquare % 8;

        for (int i = 1; i <= dir[0]; i++) { if (currentFile - i < 0) break; if (!CheckAndEnableSocket(currentSquare + (i * 7), piece)) break; }
        for (int i = 1; i <= dir[1]; i++) { if (currentFile + i > 7) break; if (!CheckAndEnableSocket(currentSquare + (i * 9), piece)) break; }
        for (int i = 1; i <= dir[2]; i++) { if (currentFile - i < 0) break; if (!CheckAndEnableSocket(currentSquare - (i * 9), piece)) break; }
        for (int i = 1; i <= dir[3]; i++) { if (currentFile + i > 7) break; if (!CheckAndEnableSocket(currentSquare - (i * 7), piece)) break; }
    }

    public void RequestChangeTurn()
    {
        if (IsServer) isWhiteTurn.Value = !isWhiteTurn.Value;
        else ChangeTurnServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void ChangeTurnServerRpc()
    {
        isWhiteTurn.Value = !isWhiteTurn.Value;
    }

    // -------------------------------------------------------------
    // INSTANT CAPTURE SYSTEM (No Combat)
    // -------------------------------------------------------------

    [ServerRpc(RequireOwnership = false)]
    public void CapturePieceServerRpc(ulong capturedPieceNetId, ulong attackingPieceNetId, int squareIndex)
    {
        CapturePieceClientRpc(capturedPieceNetId, attackingPieceNetId, squareIndex);
    }

    [ClientRpc]
    public void CapturePieceClientRpc(ulong capturedPieceNetId, ulong attackingPieceNetId, int squareIndex)
    {
        NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(capturedPieceNetId, out NetworkObject capturedPiece);
        NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(attackingPieceNetId, out NetworkObject attackingPiece);

        XRSocketInteractor targetSocket = sockets[squareIndex];

        // 1. Banish the dead piece
        if (capturedPiece != null)
        {
            IXRSelectInteractable defInteractable = capturedPiece.GetComponent<XRGrabInteractable>();
            if (targetSocket.hasSelection && targetSocket.firstInteractableSelected == defInteractable)
            {
                targetSocket.interactionManager.SelectCancel((IXRSelectInteractor)targetSocket, defInteractable);
            }
                
            capturedPiece.transform.position = new Vector3(0, -10f, 0); // Hide underground
            capturedPiece.GetComponent<Collider>().enabled = false;
        }

        // 2. Snap the attacking piece into the socket
        if (attackingPiece != null) 
        {
            IXRSelectInteractable attInteractable = attackingPiece.GetComponent<XRGrabInteractable>();
            targetSocket.StartManualInteraction(attInteractable);
        }

        // 3. Clean up the board and swap turns
        enableAllSockets();
        UpdateHandPermissions(); 
        RequestChangeTurn();     
    }

    // --- NETWORKED SOCKET SYNCING RPCS ---

    [ServerRpc(RequireOwnership = false)]
    public void ForceUnsnapServerRpc(ulong networkObjectId) { ForceUnsnapClientRpc(networkObjectId); }

    [ClientRpc]
    public void ForceUnsnapClientRpc(ulong networkObjectId)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject netObj))
        {
            IXRSelectInteractable interactable = netObj.GetComponent<XRGrabInteractable>();
            foreach (XRSocketInteractor socket in sockets)
            {
                if (socket.hasSelection && socket.firstInteractableSelected == interactable)
                {
                    socket.interactionManager.SelectCancel((IXRSelectInteractor)socket, interactable);
                    socket.GetComponent<BoxCollider>().enabled = false;
                }
                if (!netObj.IsOwner) socket.GetComponent<BoxCollider>().enabled = false;
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void SyncSnapServerRpc(ulong networkObjectId, int squareIndex) { SyncSnapClientRpc(networkObjectId, squareIndex); }

    [ClientRpc]
    public void SyncSnapClientRpc(ulong networkObjectId, int squareIndex)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject netObj))
        {
            if (netObj.IsOwner) return;
            IXRSelectInteractable interactable = netObj.GetComponent<XRGrabInteractable>();
            enableAllSockets();
            foreach (XRSocketInteractor socket in sockets)
            {
                if (socket.GetComponent<SocketTracker>().Square == squareIndex)
                {
                    socket.StartManualInteraction(interactable);
                    break;
                }
            }
        }
    }
}