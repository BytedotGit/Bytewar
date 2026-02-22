using Unity.Netcode.Components;
using UnityEngine;

namespace SurvivalRPG.Networking
{
    /// <summary>
    /// A NetworkAnimator that allows the client owner to trigger animations and set parameters.
    /// </summary>
    [DisallowMultipleComponent]
    public class ClientNetworkAnimator : NetworkAnimator
    {
        protected override bool OnIsServerAuthoritative()
        {
            return false;
        }
    }
}
