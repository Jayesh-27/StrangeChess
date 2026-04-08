using Unity.Netcode;
using UnityEngine;

public class VRAvatarSync : NetworkBehaviour
{
    public Transform avatarHead;
    public Transform avatarLeftHand;
    public Transform avatarRightHand;

    private Transform localHead;
    private Transform localLeftHand;
    private Transform localRightHand;
    private Transform XRRig;

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            localHead = GameObject.FindWithTag("MainCamera").transform;
            localLeftHand = GameObject.FindWithTag("LeftHand").transform;
            localRightHand = GameObject.FindWithTag("RightHand").transform;
            XRRig = GameObject.FindWithTag("XRRig").transform;
            transform.position = new Vector3(
                XRRig.position.x,
                XRRig.position.y,
                XRRig.position.z
            );        
        }
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
}