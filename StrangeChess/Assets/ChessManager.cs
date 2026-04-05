using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class ChessManager : NetworkBehaviour
{
    [Header("Game State")]
    [SerializeField] public bool isWhiteTurn = true;
    [SerializeField] private bool changetheTurn = false;
    [SerializeField] public bool shouldSnapBack = true;
    [SerializeField] public SocketTracker lastUnsnap = null;
    [SerializeField] private float pieceSnapTimer = 0.5f;

    [Header("Board References")]
    [SerializeField] public XRSocketInteractor[] sockets = new XRSocketInteractor[64];
    [SerializeField] private XRGrabInteractable[] whitePiecesInteractable;  // 16 pieces
    [SerializeField] private XRGrabInteractable[] blackPiecesInteractable;  // 16 pieces
    [SerializeField] private XRDirectInteractor[] interactors; // 0:whiteRight, 1:whiteLeft, 2:blackRight, 3:blackLeft

    [Header("Spawn Points")]
    [SerializeField] private GameObject XRRig;
    [SerializeField] private GameObject WhiteRigSpawnPoint;
    [SerializeField] private GameObject BlackRigSpawnPoint;

    private int[] dir = new int[4];

    private void Awake()
    {
        Debug.Log("[ChessManager] Awake: Setting up XR Interaction Listeners.");
        foreach (XRDirectInteractor interactor in interactors)
        {
            interactor.selectEntered.AddListener(onGrab);
            interactor.selectExited.AddListener(OnRelease);
        }
    }

    public override void OnNetworkSpawn()
    {
        Debug.Log($"[ChessManager] OnNetworkSpawn: IsHost = {IsHost}, ClientId = {NetworkManager.Singleton.LocalClientId}");
        
        // 1. Position the VR Rig
        if (IsHost)
        {
            XRRig.transform.position = WhiteRigSpawnPoint.transform.position;
            Debug.Log("[ChessManager] Positioned rig at White Spawn Point.");
            
            // CRITICAL FIX: Check for clients that loaded the scene at the exact same time
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client.ClientId != NetworkManager.Singleton.LocalClientId)
                {
                    GrantBlackPieceOwnership(client.ClientId);
                }
            }

            // Keep listening just in case a client connects late
            NetworkManager.Singleton.OnClientConnectedCallback += GrantBlackPieceOwnership;
        }
        else
        {
            XRRig.transform.position = BlackRigSpawnPoint.transform.position;            
            XRRig.transform.Rotate(0f, 180f, 0f);
            Debug.Log("[ChessManager] Positioned rig at Black Spawn Point.");
        }
    }

    private void GrantBlackPieceOwnership(ulong clientId)
    {
        // Ensure we don't accidentally give the host ownership again
        if (clientId != NetworkManager.Singleton.LocalClientId)
        {
            Debug.Log($"[ChessManager] Client {clientId} found! Forcing Black Pieces ownership transfer...");
            
            foreach (XRGrabInteractable interactable in blackPiecesInteractable)
            {
                NetworkObject netObj = interactable.GetComponent<NetworkObject>();
                
                // Add an extra safety check to ensure the object is ready for the network
                if (netObj != null && netObj.IsSpawned)
                {
                    netObj.ChangeOwnership(clientId);
                }
            }
            Debug.Log($"[ChessManager] SUCCESS! Client {clientId} now has full network authority over Black pieces.");
        }
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton != null && IsHost)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= GrantBlackPieceOwnership;
        }
    }

    private void Update()
    {
        if (changetheTurn)
        {
            changetheTurn = false;
            RequestChangeTurn();
        }
    }

    // --- INTERACTOR MANAGEMENT ---

    public void disableOtherDirectInteractor(XRDirectInteractor correctInteractor)
    {
        foreach (XRDirectInteractor interactor in interactors)
        {
            if (interactor != correctInteractor)
                interactor.enabled = false;
        }
    }

    public void disableOtherDirectInteractor()
    {
        foreach (XRDirectInteractor interactor in interactors)
        {
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

    // --- GRABBING & RELEASING ---

    private void onGrab(SelectEnterEventArgs args)
    {
        ChessPiece piece = args.interactableObject.transform.GetComponent<ChessPiece>();
        string pieceName = args.interactableObject.transform.name;
        Debug.Log($"[ChessManager] onGrab: Player hand grabbed -> {pieceName}");

        // 1. SECURITY CHECK: Prevent grabbing the wrong color
        if ((IsHost && !piece.isWhitePiece) || (!IsHost && piece.isWhitePiece))
        {
            Debug.LogWarning($"[ChessManager] ILLEGAL GRAB! Player tried to grab opponent's piece ({pieceName}). Forcing drop!");
            args.manager.SelectCancel(args.interactorObject, args.interactableObject);
            return; // Stop code execution here
        }

        // 2. TURN CHECK: Prevent grabbing out of turn
        if ((isWhiteTurn && !IsHost) || (!isWhiteTurn && IsHost))
        {
            Debug.LogWarning($"[ChessManager] ILLEGAL GRAB! It is not your turn!");
            args.manager.SelectCancel(args.interactorObject, args.interactableObject);
            return; // Stop code execution here
        }

        // 3. NETWORK FIX: Tell the server to force all sockets to let go of this piece
        NetworkObject netObj = args.interactableObject.transform.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            Debug.Log($"[ChessManager] Sending RPC to unsnap piece: {pieceName} (NetID: {netObj.NetworkObjectId})");
            ForceUnsnapServerRpc(netObj.NetworkObjectId);
        }

        disableOtherDirectInteractor(args.interactorObject as XRDirectInteractor);

        // 4. Move Calculation
        switch (piece.piece)
        {
            case PieceType.Pawn: pawnMoves(args.interactableObject as XRGrabInteractable); break;
            case PieceType.Knight: knightMoves(); break;
            case PieceType.Bishop: bishopMoves(); break;
            case PieceType.Rook: rookMoves(); break;
            case PieceType.Queen: queenMoves(); break;
            case PieceType.King: kingMoves(); break;
        }
    }

    void OnRelease(SelectExitEventArgs args)
    {
        Debug.Log($"[ChessManager] OnRelease: Player let go of -> {args.interactableObject.transform.name}");
        StartCoroutine(snapPieceBack(args.interactableObject as XRGrabInteractable));
    }

    IEnumerator snapPieceBack(XRGrabInteractable grab)
    {
        disableOtherDirectInteractor();
        yield return new WaitForSeconds(pieceSnapTimer);
        
        if (shouldSnapBack && lastUnsnap != null)
        {
            Debug.Log($"[ChessManager] snapPieceBack: Piece {grab.transform.name} snapping back to square {lastUnsnap.Square}");
            if (!lastUnsnap.enabled)
                lastUnsnap.enabled = true;
                
            lastUnsnap.GetComponent<XRSocketInteractor>().StartManualInteraction((IXRSelectInteractable)grab);
        }
        else
        {
            Debug.Log($"[ChessManager] snapPieceBack: Piece successfully placed on new square! (shouldSnapBack = {shouldSnapBack})");
            
            // The piece was successfully placed on a NEW square. Move is over, change turn!
            RequestChangeTurn();
        }
        
        enableDirectInteractor();
    }

    // --- PIECE MOVEMENT LOGIC ---

    private void pawnMoves(XRGrabInteractable grab)
    {
        int currentSquare = lastUnsnap.Square;
        int currentRow = currentSquare / 8;
        disableAllSockets(lastUnsnap.GetComponent<XRSocketInteractor>());

        if (grab.GetComponent<ChessPiece>().isWhitePiece)
        {
            dir[0] = (currentRow == 1) ? 2 : 1;
            dir[1] = 0;
        }
        else
        {
            dir[0] = 0;
            dir[1] = (currentRow == 6) ? 2 : 1;
        }
        dir[2] = 0; dir[3] = 0;
        straightMoves();
    }

    private void knightMoves()
    {
        disableAllSockets(lastUnsnap.GetComponent<XRSocketInteractor>());
        int currentSquare = lastUnsnap.Square;
        int currentFile = currentSquare % 8;
        int currentRow = currentSquare / 8;
        int[] knightOffsets = { 15, 17, 10, -6, -15, -17, -10, 6 };

        foreach (int offset in knightOffsets)
        {
            int target = currentSquare + offset;
            if (target >= 0 && target < 64)
            {
                int targetFile = target % 8;
                int targetRow = target / 8;
                int fileDiff = Mathf.Abs(currentFile - targetFile);
                int rowDiff = Mathf.Abs(currentRow - targetRow);

                if ((fileDiff == 1 && rowDiff == 2) || (fileDiff == 2 && rowDiff == 1))
                {
                    if (sockets[target] != null && !sockets[target].hasSelection)
                    {
                        sockets[target].GetComponent<BoxCollider>().enabled = true;
                    }
                }
            }
        }
    }

    private void bishopMoves()
    {
        disableAllSockets(lastUnsnap.GetComponent<XRSocketInteractor>());
        dir[0] = 8; dir[1] = 8; dir[2] = 8; dir[3] = 8;
        diagonalMoves();
    }

    private void rookMoves()
    {
        disableAllSockets(lastUnsnap.GetComponent<XRSocketInteractor>());
        dir[0] = 8; dir[1] = 8; dir[2] = 8; dir[3] = 8;
        straightMoves();
    }

    private void queenMoves()
    {
        disableAllSockets(lastUnsnap.GetComponent<XRSocketInteractor>());
        dir[0] = 8; dir[1] = 8; dir[2] = 8; dir[3] = 8;
        straightMoves();
        diagonalMoves();
    }

    private void kingMoves()
    {
        disableAllSockets(lastUnsnap.GetComponent<XRSocketInteractor>());
        dir[0] = 1; dir[1] = 1; dir[2] = 1; dir[3] = 1;
        straightMoves();
        diagonalMoves();
    }

    private void straightMoves()
    {
        int currentSquare = lastUnsnap.Square;
        int currentFile = currentSquare % 8;

        for (int i = 1; i <= dir[0]; i++) // UP
        {
            int target = currentSquare + (i * 8);
            if (target >= 64 || sockets[target] == null || sockets[target].hasSelection) break;
            sockets[target].GetComponent<BoxCollider>().enabled = true;
        }
        for (int i = 1; i <= dir[1]; i++) // DOWN
        {
            int target = currentSquare - (i * 8);
            if (target < 0 || sockets[target] == null || sockets[target].hasSelection) break;
            sockets[target].GetComponent<BoxCollider>().enabled = true;
        }
        for (int i = 1; i <= dir[2]; i++) // RIGHT
        {
            int target = currentSquare + i;
            if (currentFile + i > 7 || target >= 64 || sockets[target] == null || sockets[target].hasSelection) break;
            sockets[target].GetComponent<BoxCollider>().enabled = true;
        }
        for (int i = 1; i <= dir[3]; i++) // LEFT
        {
            int target = currentSquare - i;
            if (currentFile - i < 0 || target < 0 || sockets[target] == null || sockets[target].hasSelection) break;
            sockets[target].GetComponent<BoxCollider>().enabled = true;
        }
    }

    private void diagonalMoves()
    {
        int currentSquare = lastUnsnap.Square;
        int currentFile = currentSquare % 8;

        for (int i = 1; i <= dir[0]; i++) // UP-LEFT
        {
            int target = currentSquare + (i * 7);
            if (currentFile - i < 0 || target >= 64 || sockets[target] == null || sockets[target].hasSelection) break;
            sockets[target].GetComponent<BoxCollider>().enabled = true;
        }
        for (int i = 1; i <= dir[1]; i++) // UP-RIGHT
        {
            int target = currentSquare + (i * 9);
            if (currentFile + i > 7 || target >= 64 || sockets[target] == null || sockets[target].hasSelection) break;
            sockets[target].GetComponent<BoxCollider>().enabled = true;
        }
        for (int i = 1; i <= dir[2]; i++) // DOWN-LEFT
        {
            int target = currentSquare - (i * 9);
            if (currentFile - i < 0 || target < 0 || sockets[target] == null || sockets[target].hasSelection) break;
            sockets[target].GetComponent<BoxCollider>().enabled = true;
        }
        for (int i = 1; i <= dir[3]; i++) // DOWN-RIGHT
        {
            int target = currentSquare - (i * 7);
            if (currentFile + i > 7 || target < 0 || sockets[target] == null || sockets[target].hasSelection) break;
            sockets[target].GetComponent<BoxCollider>().enabled = true;
        }
    }

    // --- SOCKET MANAGEMENT ---

    private void disableAllSockets(XRSocketInteractor lastUnsnapSocket)
    {
        foreach (XRSocketInteractor socket in sockets)
        {
            socket.GetComponent<BoxCollider>().enabled = (lastUnsnapSocket == socket);
        }
    }

    public void enableAllSockets()
    {
        foreach (XRSocketInteractor socket in sockets)
        {
            socket.GetComponent<BoxCollider>().enabled = true;
        }
    }

    // -------------------------------------------------------------
    // TURN MANAGEMENT RPCS
    // -------------------------------------------------------------

    public void RequestChangeTurn()
    {
        if (IsServer)
        {
            ChangeTurnClientRpc();
        }
        else
        {
            ChangeTurnServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void ChangeTurnServerRpc()
    {
        ChangeTurnClientRpc();
    }

    [ClientRpc]
    private void ChangeTurnClientRpc()
    {
        isWhiteTurn = !isWhiteTurn;
        string turnStatus = isWhiteTurn ? "White's" : "Black's";
        Debug.Log($"[ChessManager] Turn Changed! It is now {turnStatus} turn.");
    }

    // -------------------------------------------------------------
    // NETWORKED SOCKET SYNCING RPCS
    // -------------------------------------------------------------

    [ServerRpc(RequireOwnership = false)]
    public void ForceUnsnapServerRpc(ulong networkObjectId)
    {
        Debug.Log($"[ChessManager] RPC SERVER: Received request to unsnap NetworkID {networkObjectId}. Broadcasting to clients...");
        ForceUnsnapClientRpc(networkObjectId);
    }

    [ClientRpc]
    public void ForceUnsnapClientRpc(ulong networkObjectId)
    {
        Debug.Log($"[ChessManager] RPC CLIENT: Searching for NetworkID {networkObjectId} to force drop...");
        
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject netObj))
        {
            IXRSelectInteractable interactable = netObj.GetComponent<XRGrabInteractable>();
            
            foreach (XRSocketInteractor socket in sockets)
            {
                if (socket.hasSelection && socket.firstInteractableSelected == interactable)
                {
                    Debug.Log($"[ChessManager] SUCCESS! Socket {socket.transform.name} is holding it. Forcing drop and blinding socket.");
                    socket.interactionManager.SelectCancel((IXRSelectInteractor)socket, interactable);
                    socket.GetComponent<BoxCollider>().enabled = false;
                }

                // --- CRITICAL FIX: BLIND THE OPPONENT'S BOARD ---
                // If the opponent is the one holding this piece, turn off all of OUR sockets.
                // This prevents our board from accidentally catching the piece mid-air!
                if (!netObj.IsOwner)
                {
                    socket.GetComponent<BoxCollider>().enabled = false;
                }
            }
        }
        else
        {
            Debug.LogWarning($"[ChessManager] RPC FAILED: Could not find NetworkObject with ID {networkObjectId} in SpawnManager!");
        }
    }

    // -------------------------------------------------------------
    // NEW: NETWORKED DROP SYNCING
    // -------------------------------------------------------------

    [ServerRpc(RequireOwnership = false)]
    public void SyncSnapServerRpc(ulong networkObjectId, int squareIndex)
    {
        SyncSnapClientRpc(networkObjectId, squareIndex);
    }

    [ClientRpc]
    public void SyncSnapClientRpc(ulong networkObjectId, int squareIndex)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject netObj))
        {
            // If we are the player who dropped the piece, we already snapped it locally. Do nothing.
            if (netObj.IsOwner) return;

            IXRSelectInteractable interactable = netObj.GetComponent<XRGrabInteractable>();
            
            // Turn all of our sockets back on now that the opponent is done moving
            enableAllSockets();

            // Find the specific socket the opponent dropped it in, and force our board to snap it there
            foreach (XRSocketInteractor socket in sockets)
            {
                if (socket.GetComponent<SocketTracker>().Square == squareIndex)
                {
                    socket.StartManualInteraction(interactable);
                    Debug.Log($"[Network Sync] Successfully synchronized {netObj.name} into Square {squareIndex}");
                    break;
                }
            }
        }
    }
}