using Unity.Netcode.Components;
using UnityEngine;

[DisallowMultipleComponent]
public class ClientNetworkTransform : NetworkTransform
{
    // This single line is the magic trick. It tells Netcode:
    // "Do not let the server control this, let the owner (client) control it."
    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }
}