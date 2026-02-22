namespace ByteWar.Core
{
    /// <summary>
    /// Any world object the player can interact with (gather, open, activate).
    /// Implemented by ResourceNode, CraftingStation, etc.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>Display name shown in interaction prompts.</summary>
        string InteractionName { get; }

        /// <summary>Whether this object can currently be interacted with by the given client.</summary>
        bool CanInteract(ulong clientId);

        /// <summary>Perform the interaction. Must be called on the server.</summary>
        void Interact(ulong clientId);
    }
}
