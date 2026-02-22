namespace ByteWar.Core
{
    /// <summary>
    /// Any entity whose state can be serialized for persistence.
    /// Implemented by WorldPersistence, BuildingPiece.
    /// </summary>
    public interface IPersistable
    {
        /// <summary>Serialize this entity's state to a JSON string.</summary>
        string Serialize();

        /// <summary>Restore this entity's state from a JSON string.</summary>
        void Deserialize(string data);
    }
}
