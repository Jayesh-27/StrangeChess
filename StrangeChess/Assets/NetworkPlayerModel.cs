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
    [SerializeField] private Transform leftHand;
    [SerializeField] private Transform rightHand;

    private void Start()
    {
        CenterEyeAnchor = GameObject.Find("CenterEyeAnchor").transform;
        LeftHandAnchor = GameObject.Find("LeftHandAnchor").transform;
        RightHandAnchor = GameObject.Find("RightHandAnchor").transform;
    }

    private void Update()
    {
        head.position = CenterEyeAnchor.position;
        leftHand.position = LeftHandAnchor.position;
        rightHand.position = RightHandAnchor.position;
    }
}