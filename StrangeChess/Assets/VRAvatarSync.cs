using Unity.Netcode;
using UnityEngine;

public class VRAvatarSync : NetworkBehaviour
{
    [Header("Avatar Transforms (Drag these from the Prefab)")]
    public Transform avatarHead;
    public Transform avatarLeftHand;
    public Transform avatarRightHand;

    // These will hold the real VR hardware transforms
    private Transform localHead;
    private Transform localLeftHand;
    private Transform localRightHand;

    public override void OnNetworkSpawn()
    {
        // We only want to link the VR hardware if WE own this specific avatar
        if (IsOwner)
        {
            // Find our local XR Origin parts using the tags we set up
            localHead = Camera.main.transform;
            localLeftHand = GameObject.FindWithTag("LeftHand").transform;
            localRightHand = GameObject.FindWithTag("RightHand").transform;

            // OPTIONAL: Hide our own avatar meshes so they don't block our camera view!
            // (You usually don't want to see the inside of your own avatar's head)
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>())
            {
                renderer.enabled = false;
            }
        }
    }

    void Update()
    {
        // Every frame, if we own this avatar and we successfully found our VR rig...
        if (IsOwner && localHead != null)
        {
            // 1. Move the root of the avatar to the floor directly under the headset
            transform.position = new Vector3(localHead.position.x, 0, localHead.position.z);

            // 2. Make the avatar head match the real headset
            avatarHead.position = localHead.position;
            avatarHead.rotation = localHead.rotation;

            // 3. Make the avatar hands match the real controllers
            avatarLeftHand.position = localLeftHand.position;
            avatarLeftHand.rotation = localLeftHand.rotation;

            avatarRightHand.position = localRightHand.position;
            avatarRightHand.rotation = localRightHand.rotation;
        }
    }
}