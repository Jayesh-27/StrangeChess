using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NetworkPlayerModel : NetworkBehaviour
{
    [SerializeField] private Transform CenterEyeAnchor;
    [SerializeField] private Transform LeftHandAnchor;
    [SerializeField] private Transform RightHandAnchor;
    [SerializeField] private Transform head;
    [SerializeField] private GameObject headMesh1;

    [SerializeField] private GameObject headMesh2;
    [SerializeField] private Transform leftHand;
    [SerializeField] private Transform rightHand;

    private void Start()
    {
        CenterEyeAnchor = GameObject.Find("CenterEyeAnchor").transform;
        LeftHandAnchor = GameObject.Find("LeftHandAnchor").transform;
        RightHandAnchor = GameObject.Find("RightHandAnchor").transform;

        if(IsOwner)
        {
            head.gameObject.GetComponent<BoxCollider>().enabled = false;
            headMesh1.gameObject.GetComponent<MeshRenderer>().enabled = false;
            headMesh2.gameObject.GetComponent<MeshRenderer>().enabled = false;
            leftHand.gameObject.GetComponent<MeshRenderer>().enabled = false;
            rightHand.gameObject.GetComponent<MeshRenderer>().enabled = false;
        }
    }

    private void Update()
    {
        head.position = CenterEyeAnchor.position;
        leftHand.position = LeftHandAnchor.position;
        rightHand.position = RightHandAnchor.position;

        head.rotation = CenterEyeAnchor.rotation;
        leftHand.rotation = LeftHandAnchor.rotation;
        rightHand.rotation = RightHandAnchor.rotation;        
    }
}