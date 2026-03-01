using Unity.Netcode.Components;
using UnityEngine;

namespace SimpleVoxelSystem.Net
{
    /// <summary>
    /// NetworkTransform that allows client authority.
    /// Used for Player movement from client side.
    /// </summary>
    [DisallowMultipleComponent]
    public class ClientNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative()
        {
            return false;
        }
    }
}
