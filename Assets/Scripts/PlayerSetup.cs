using System.Diagnostics.Tracing;
using Unity.Netcode;
using UnityEngine;
public class PlayerSetup : NetworkBehaviour
{
    [SerializeField] private GameObject CameraRig;
    [SerializeField] private GameObject NetworkPlayerModel;

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            Instantiate(CameraRig, transform);
        }
        var model = Instantiate(NetworkPlayerModel, transform);
        //model.SetActive(!IsOwner);
    }    
}