namespace ByteWar.Core
{
    /// <summary>
    /// Enumerates all SFX clip types used by <see cref="AudioManager"/>.
    /// Corresponds 1-to-1 with the clip slots in AudioManager.
    /// </summary>
    public enum SFXType
    {
        Footstep       = 0,
        MeleeHit       = 1,
        FireballCast   = 2,
        FireballImpact = 3,
        ItemPickup     = 4,
        BuildingPlace  = 5,
        UIClick        = 6,
        CleaveHit      = 7,
    }
}
