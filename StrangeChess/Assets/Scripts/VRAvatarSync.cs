using Unity.Netcode;
using UnityEngine;

public class VRAvatarSync : NetworkBehaviour
{
    [Header("Avatar Transforms")]
    public Transform avatarHead;
    public Transform avatarLeftHand;
    public Transform avatarRightHand;

    [Header("Combat Assets")]
    public GameObject pawnMesh;
    public GunController equippedGun;

    private Transform localHead;
    private Transform localLeftHand;
    private Transform localRightHand;
    private Transform XRRig;

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            // We use a quick check to prevent null errors if the camera hasn't loaded yet
            GameObject cam = GameObject.FindWithTag("MainCamera");
            if (cam != null)
            {
                localHead = cam.transform;
                localLeftHand = GameObject.FindWithTag("LeftHand").transform;
                localRightHand = GameObject.FindWithTag("RightHand").transform;
                XRRig = GameObject.FindWithTag("XRRig").transform;
                transform.position = new Vector3(
                    XRRig.position.x,
                    XRRig.position.y,
                    XRRig.position.z
                );
            }

            // Hide our own combat meshes so they don't block our camera view
            if (pawnMesh != null)
            {
                foreach (var renderer in pawnMesh.GetComponentsInChildren<MeshRenderer>())
                {
                    renderer.enabled = false;
                }
            }
        }
        
        DisableCombatMode();
    }

    void Update()
    {
        if (IsOwner && localHead != null)
        {            
            avatarHead.localPosition = localHead.localPosition;
            avatarHead.localRotation = localHead.localRotation;

            avatarLeftHand.localPosition = localLeftHand.localPosition;
            avatarLeftHand.localRotation = localLeftHand.localRotation;

            avatarRightHand.localPosition = localRightHand.localPosition;
            avatarRightHand.localRotation = localRightHand.localRotation;
        }
        // YOUR CUSTOM OFFSET LOGIC (Preserved exactly as requested)
        else if(IsOwner && localHead == null)
        {
            localHead = GameObject.FindWithTag("MainCamera").transform;
            localLeftHand = GameObject.FindWithTag("LeftHand").transform;
            localRightHand = GameObject.FindWithTag("RightHand").transform;
            XRRig = GameObject.FindWithTag("XRRig").transform;
            transform.position = new Vector3(
                XRRig.position.x,
                XRRig.position.y + 1f,
                XRRig.position.z
            ); 
        }
    }

    // --- COMBAT TOGGLES ---
    
    public void EnableCombatMode(PieceType type)
    {
        if (type == PieceType.Pawn && pawnMesh != null) pawnMesh.SetActive(true);
        
        if (equippedGun != null) 
        {
            equippedGun.gameObject.SetActive(true);
            equippedGun.canFire = false; // Locked until countdown finishes
        }
    }

    public void DisableCombatMode()
    {
        if (pawnMesh != null) pawnMesh.SetActive(false);
        if (equippedGun != null) equippedGun.gameObject.SetActive(false);
    }
}