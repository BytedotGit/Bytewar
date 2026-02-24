namespace ByteWar.Core
{
    /// <summary>
    /// Enumerates all VFX clip types used by <see cref="VFXManager"/>.
    /// Corresponds 1-to-1 with the prefab slots in VFXManager.
    /// </summary>
    public enum VFXType
    {
        FireballMuzzle    = 0,
        FireballImpact    = 1,
        GatherHit         = 2,
        ResourceDeath     = 3,
        BuildingPlace     = 4,
        ItemPickup        = 5,
        CleaveHit         = 6,
    }
}
