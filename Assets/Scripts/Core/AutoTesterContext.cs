using ByteWar.Networking;
using Unity.Netcode;
using UnityEngine;

namespace ByteWar.Core
{
    /// <summary>
    /// Shared context passed to all AutoTester scenarios, containing references
    /// to the local player, camera, input handler, and other core objects.
    /// </summary>
    public class AutoTesterContext
    {
        public NetworkPlayer LocalPlayer;
        public PlayerInputHandler InputHandler;
        public Camera MainCamera;
        public ThirdPersonCamera ThirdPersonCamera;
        public Animator Animator;
        public CharacterController CharacterController;
        public NetworkManager NetworkManager;
    }
}
