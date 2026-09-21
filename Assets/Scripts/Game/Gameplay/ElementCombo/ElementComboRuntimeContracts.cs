using System.Collections.Generic;
using Game.Contracts;
using UnityEngine;

namespace Game.Gameplay
{
    public readonly struct ElementComboHitRequest
    {
        public ElementComboHitRequest(
            int levelRunId,
            int primaryTargetRuntimeId,
            Vector2 primaryTargetCenter,
            BulletDamageContext directDamage)
        {
            LevelRunId = levelRunId;
            PrimaryTargetRuntimeId = primaryTargetRuntimeId;
            PrimaryTargetCenter = primaryTargetCenter;
            DirectDamage = directDamage;
        }

        public int LevelRunId { get; }
        public int PrimaryTargetRuntimeId { get; }
        public Vector2 PrimaryTargetCenter { get; }
        public BulletDamageContext DirectDamage { get; }
    }

    public readonly struct EnemyEffectTargetSnapshot
    {
        public EnemyEffectTargetSnapshot(int runtimeInstanceId, Vector2 center)
        {
            RuntimeInstanceId = runtimeInstanceId;
            Center = center;
        }

        public int RuntimeInstanceId { get; }
        public Vector2 Center { get; }
    }

    public interface IEnemyEffectService
    {
        bool TryGetAliveEffectTarget(
            int levelRunId,
            int runtimeInstanceId,
            out EnemyEffectTargetSnapshot target);

        void CollectAliveEffectTargets(
            int levelRunId,
            Vector2 center,
            float radius,
            List<EnemyEffectTargetSnapshot> results);

        bool ApplyEnemyDamage(
            int levelRunId,
            int runtimeInstanceId,
            EnemyDamageContext damage);

        void QueueEnemyDisplacement(
            int levelRunId,
            int runtimeInstanceId,
            Vector2 displacement);

        bool TryGetRecentElementMask(
            int levelRunId,
            int runtimeInstanceId,
            out ElementMask elements);
    }

    public interface IElementComboResolver
    {
        void StartRun(int levelRunId);
        bool TryResolveEnemyHit(ElementComboHitRequest request);
        void StopRun(int levelRunId);
    }
}
