using System.Collections.Generic;

namespace Game.Contracts
{
    public interface IArmyConfigProvider
    {
        ArmyConfigSnapshot GetArmyConfig(int armyId);
    }

    public interface IWeaponConfigProvider
    {
        WeaponConfigSnapshot GetWeaponConfig(int weaponId);
    }

    public interface IBulletConfigProvider
    {
        BulletConfigSnapshot GetBulletConfig(int bulletId);
    }

    public interface IEnemyConfigProvider
    {
        EnemyConfigSnapshot GetEnemyConfig(int enemyId);
    }

    public interface IPropConfigProvider
    {
        PropConfigSnapshot GetPropConfig(int propId);
    }

    public interface IConfigService :
        IArmyConfigProvider,
        IWeaponConfigProvider,
        IBulletConfigProvider,
        IEnemyConfigProvider,
        IPropConfigProvider
    {
        ConfigLoadState GetConfigLoadState();
        IReadOnlyList<LevelDescriptor> GetLevelDescriptors();
        bool TryGetLevelConfig(int levelId, out LevelConfigSnapshot levelConfig);
    }
}
