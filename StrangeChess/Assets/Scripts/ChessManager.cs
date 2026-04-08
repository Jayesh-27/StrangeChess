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

    [Header("Combat UI & State")]
    public TMPro.TMP_Text combatCountdownText;
    private ulong currentAttackerNetId;
    private ulong currentDefenderNetId;
    private int currentContestedSquare;

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
        if (combatCountdownText != null) combatCountdownText.gameObject.SetActive(false);
    }

    public override void OnNetworkSpawn()
    {
        XROrigin xrOrigin = XRRig.GetComponent<XROrigin>();

        isWhiteTurn.OnValueChanged += (bool prev, bool current) => {
            UpdateHandPermissions(); 
        };

        if (IsHost)
        {
            xrOrigin.MoveCameraToWorldLocation(WhiteRigSpawnPoint.transform.position);
            xrOrigin.MatchOriginUpCameraForward(WhiteRigSpawnPoint.transform.up, WhiteRigSpawnPoint.transform.forward);
            
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client.ClientId != NetworkManager.Singleton.LocalClientId) GrantBlackPieceOwnership(client.ClientId);
            }
            NetworkManager.Singleton.OnClientConnectedCallback += GrantBlackPieceOwnership;
        }
        else
        {
            xrOrigin.MoveCameraToWorldLocation(BlackRigSpawnPoint.transform.position);            
            xrOrigin.MatchOriginUpCameraForward(BlackRigSpawnPoint.transform.up, BlackRigSpawnPoint.transform.forward);
        }

        UpdateHandPermissions();
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

    // PIECE MOVEMENT LOGIC
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

    private void knightMoves(ChessPiece piece) { /* Unchanged */ }
    private void bishopMoves(ChessPiece piece) { /* Unchanged */ }
    private void rookMoves(ChessPiece piece) { /* Unchanged */ }
    private void queenMoves(ChessPiece piece) { /* Unchanged */ }
    private void kingMoves(ChessPiece piece) { /* Unchanged */ }
    private void straightMoves(ChessPiece piece) { /* Unchanged */ }
    private void diagonalMoves(ChessPiece piece) { /* Unchanged */ }


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
    // COMBAT TRANSITION SYSTEM
    // -------------------------------------------------------------

    // We changed the name of this from CapturePieceServerRpc! Update SocketTracker to call this!
    [ServerRpc(RequireOwnership = false)]
    public void StartCombatServerRpc(ulong capturedPieceNetId, ulong attackingPieceNetId, int squareIndex)
    {
        StartCombatClientRpc(capturedPieceNetId, attackingPieceNetId, squareIndex);
    }

    [ClientRpc]
    public void StartCombatClientRpc(ulong capturedPieceNetId, ulong attackingPieceNetId, int squareIndex)
    {
        // 1. Save the fight info for when the match ends
        currentDefenderNetId = capturedPieceNetId;
        currentAttackerNetId = attackingPieceNetId;
        currentContestedSquare = squareIndex;

        // 2. Lock the chess board so players can't move pieces during the shootout
        disableOtherDirectInteractor(); 
        
        // 3. Force the attacker's hand to let go of the piece they are holding
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(attackingPieceNetId, out NetworkObject attObj))
        {
            IXRSelectInteractable attInteractable = attObj.GetComponent<XRGrabInteractable>();
            foreach (XRDirectInteractor interactor in interactors)
            {
                if (interactor.hasSelection && interactor.firstInteractableSelected == attInteractable)
                    interactor.interactionManager.SelectCancel((IXRSelectInteractor)interactor, attInteractable);
            }
        }

        // 4. Turn on the Avatars and Guns!
        // We find the local player's avatar and turn it on. For MVP, we force it to PieceType.Pawn.
        VRAvatarSync[] avatars = FindObjectsOfType<VRAvatarSync>();
        foreach (VRAvatarSync avatar in avatars)
        {
            if (avatar.IsOwner) avatar.EnableCombatMode(PieceType.Pawn);
        }

        // 5. Start the countdown
        StartCoroutine(CombatCountdownRoutine());
    }

    private IEnumerator CombatCountdownRoutine()
    {
        if (combatCountdownText != null)
        {
            combatCountdownText.gameObject.SetActive(true);
            
            combatCountdownText.text = "3";
            yield return new WaitForSeconds(1f);
            
            combatCountdownText.text = "2";
            yield return new WaitForSeconds(1f);
            
            combatCountdownText.text = "1";
            yield return new WaitForSeconds(1f);
            
            combatCountdownText.text = "FIGHT!";
            yield return new WaitForSeconds(1f);
            
            combatCountdownText.gameObject.SetActive(false);
        }
        else
        {
            yield return new WaitForSeconds(3f); // Fallback if UI is missing
        }

        // UNLOCK GUNS!
        VRAvatarSync[] avatars = FindObjectsOfType<VRAvatarSync>();
        foreach (VRAvatarSync avatar in avatars)
        {
            if (avatar.IsOwner && avatar.equippedGun != null) 
                avatar.equippedGun.canFire = true;
        }
    }

    // -------------------------------------------------------------
    // COMBAT RESOLUTION SYSTEM
    // -------------------------------------------------------------

    [ServerRpc(RequireOwnership = false)]
    public void ResolveCombatServerRpc(ulong deadPlayerClientId)
    {
        ResolveCombatClientRpc(deadPlayerClientId);
    }

    [ClientRpc]
    public void ResolveCombatClientRpc(ulong deadPlayerClientId)
    {
        NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(currentAttackerNetId, out NetworkObject attackerPiece);
        NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(currentDefenderNetId, out NetworkObject defenderPiece);

        XRSocketInteractor targetSocket = sockets[currentContestedSquare];

        // Did the Attacker die?
        if (attackerPiece != null && attackerPiece.OwnerClientId == deadPlayerClientId)
        {
            // Banish the Attacker. Defender stays exactly where they are.
            attackerPiece.transform.position = new Vector3(0, -10f, 0);
            attackerPiece.GetComponent<Collider>().enabled = false;
        }
        // Did the Defender die?
        else if (defenderPiece != null)
        {
            // Banish Defender
            IXRSelectInteractable defInteractable = defenderPiece.GetComponent<XRGrabInteractable>();
            if (targetSocket.hasSelection && targetSocket.firstInteractableSelected == defInteractable)
                targetSocket.interactionManager.SelectCancel((IXRSelectInteractor)targetSocket, defInteractable);
                
            defenderPiece.transform.position = new Vector3(0, -10f, 0);
            defenderPiece.GetComponent<Collider>().enabled = false;

            // Snap Attacker to the square
            IXRSelectInteractable attInteractable = attackerPiece.GetComponent<XRGrabInteractable>();
            targetSocket.StartManualInteraction(attInteractable);
        }

        // Clean up Combat Phase
        enableAllSockets();
        UpdateHandPermissions(); // Turns hands back on for chess
        RequestChangeTurn();     // Next player's turn!

        // Hide Avatars and Guns, Reset Health
        VRAvatarSync[] avatars = FindObjectsOfType<VRAvatarSync>();
        foreach (VRAvatarSync avatar in avatars)
        {
            if (avatar.IsOwner) 
            {
                avatar.DisableCombatMode();
                avatar.GetComponent<PlayerHealth>().ResetHealth();
            }
        }
    }

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