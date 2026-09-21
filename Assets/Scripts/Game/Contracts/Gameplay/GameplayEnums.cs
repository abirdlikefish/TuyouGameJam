using System;

namespace Game.Contracts
{
    public enum EnemyType
    {
        Chick = 0,
        Hen = 1,
        Rooster = 2
    }

    public enum AttackType
    {
        SingleTarget = 0,
        Area = 1
    }

    public enum GateType
    {
        Additive = 0,
        Element = 1
    }

    public enum ObstacleKind
    {
        Gate = 0,
        Prop = 1
    }

    public enum ObstacleState
    {
        MovingDown = 0,
        ContactPending = 1,
        ContactSucceeded = 2,
        ContactFailed = 3,
        ExitedUncontacted = 4,
        Broken = 5,
        Recycled = 6
    }

    public enum GateContactState
    {
        Pending = 0,
        Succeeded = 1,
        Failed = 2,
        ExitedUncontacted = 3
    }

    public enum PropContactState
    {
        Pending = 0,
        Succeeded = 1,
        Failed = 2,
        ExitedUncontacted = 3
    }

    public enum ArmyCountChangeReason
    {
        Addition = 0,
        Damage = 1,
        RemovalRequest = 2
    }

    public enum ArmyReachedZeroReason
    {
        Damage = 0,
        RemovalRequest = 1
    }

    public enum ArmyWeaponChangeReason
    {
        WeaponPickup = 0,
        ElementActivated = 1,
        ElementExpired = 2
    }

    public enum ObstacleRecycleReason
    {
        ContactResolved = 0,
        Broken = 1,
        ExitedRoad = 2,
        StopRun = 3
    }

    public enum ElementType
    {
        None = 0,
        Fire = 1,
        Ice = 2,
        Lightning = 3
    }

    public enum ElementComboKind
    {
        None = 0,
        FireIceSteam = 1,
        FireLightningExplosion = 2,
        IceLightningChain = 3
    }

    [Flags]
    public enum ElementMask : byte
    {
        None = 0,
        Fire = 1 << 0,
        Ice = 1 << 1,
        Lightning = 1 << 2
    }

    public enum LevelRunState
    {
        Preparing = 0,
        Playing = 1,
        Completed = 2
    }

    public enum SpawnKind
    {
        Enemy = 0,
        Gate = 1,
        Prop = 2
    }
}
