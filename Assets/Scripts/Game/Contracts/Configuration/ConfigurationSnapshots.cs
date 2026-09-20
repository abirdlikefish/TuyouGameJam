namespace Game.Contracts
{
    public readonly struct LevelDescriptor
    {
        public LevelDescriptor(int levelId, string displayName, bool initiallyUnlocked)
        {
            LevelId = levelId;
            DisplayName = displayName;
            InitiallyUnlocked = initiallyUnlocked;
        }

        public int LevelId { get; }
        public string DisplayName { get; }
        public bool InitiallyUnlocked { get; }
    }

    public readonly struct ArmyConfigSnapshot
    {
        public ArmyConfigSnapshot(int id, int armyCountLimit, int hpPerSoldier, float moveSpeed)
        {
            Id = id;
            ArmyCountLimit = armyCountLimit;
            HpPerSoldier = hpPerSoldier;
            MoveSpeed = moveSpeed;
        }

        public int Id { get; }
        public int ArmyCountLimit { get; }
        public int HpPerSoldier { get; }
        public float MoveSpeed { get; }
    }

    public readonly struct WeaponConfigSnapshot
    {
        public WeaponConfigSnapshot(int id, float fireInterval, int bulletId)
        {
            Id = id;
            FireInterval = fireInterval;
            BulletId = bulletId;
        }

        public int Id { get; }
        public float FireInterval { get; }
        public int BulletId { get; }
    }

    public readonly struct BulletConfigSnapshot
    {
        public BulletConfigSnapshot(int id, int damage, float moveSpeed)
        {
            Id = id;
            Damage = damage;
            MoveSpeed = moveSpeed;
        }

        public int Id { get; }
        public int Damage { get; }
        public float MoveSpeed { get; }
    }

    public readonly struct EnemyConfigSnapshot
    {
        public EnemyConfigSnapshot(
            int id,
            EnemyType enemyType,
            int maxHp,
            int attackPower,
            float moveSpeed,
            float attackStartRange,
            float attackCooldown)
        {
            Id = id;
            EnemyType = enemyType;
            MaxHp = maxHp;
            AttackPower = attackPower;
            MoveSpeed = moveSpeed;
            AttackStartRange = attackStartRange;
            AttackCooldown = attackCooldown;
        }

        public int Id { get; }
        public EnemyType EnemyType { get; }
        public int MaxHp { get; }
        public int AttackPower { get; }
        public float MoveSpeed { get; }
        public float AttackStartRange { get; }
        public float AttackCooldown { get; }
    }

    public readonly struct PropConfigSnapshot
    {
        public PropConfigSnapshot(
            int id,
            int weaponId,
            int maxHp,
            int contactDamage,
            float moveSpeed)
        {
            Id = id;
            WeaponId = weaponId;
            MaxHp = maxHp;
            ContactDamage = contactDamage;
            MoveSpeed = moveSpeed;
        }

        public int Id { get; }
        public int WeaponId { get; }
        public int MaxHp { get; }
        public int ContactDamage { get; }
        public float MoveSpeed { get; }
    }
}
