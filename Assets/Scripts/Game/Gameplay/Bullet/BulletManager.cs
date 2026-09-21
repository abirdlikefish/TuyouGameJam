using System;
using System.Collections.Generic;
using Game.Contracts;
using UnityEngine;

namespace Game.Gameplay
{
    public sealed class BulletManager : MonoBehaviour, IBulletManager
    {
        [SerializeField] private Bullet bulletPrefab;
        [SerializeField] private LayerMask hittableLayers;

        private readonly List<Bullet> activeBullets = new List<Bullet>();
        private readonly HashSet<Bullet> pendingRecycles = new HashSet<Bullet>();
        private readonly RaycastHit2D[] castResults = new RaycastHit2D[64];
        private readonly Dictionary<long, HitCandidate> candidatesByRuntimeId =
            new Dictionary<long, HitCandidate>();

        private IComponentPool<Bullet> pool;
        private IBulletConfigProvider configProvider;
        private int levelRunId;
        private int nextRuntimeInstanceId;
        private float topBoundary;
        private bool initialized;
        private bool running;

        public void Initialize(IPoolService poolService, IBulletConfigProvider bulletConfigProvider)
        {
            if (poolService == null)
            {
                throw new ArgumentNullException(nameof(poolService));
            }

            if (bulletConfigProvider == null)
            {
                throw new ArgumentNullException(nameof(bulletConfigProvider));
            }

            if (initialized)
            {
                throw new InvalidOperationException("BulletManager is already initialized.");
            }

            if (!TryValidate(out var error))
            {
                throw new InvalidOperationException(error);
            }

            pool = poolService.GetOrCreatePool(bulletPrefab);
            configProvider = bulletConfigProvider;
            initialized = true;
        }

        public bool TryValidate(out string error)
        {
            if (bulletPrefab == null)
            {
                error = $"{name}.bulletPrefab is not assigned.";
                return false;
            }

            if (hittableLayers.value == 0)
            {
                error = $"{name}.hittableLayers must include EnemyBody, Gate and Prop.";
                return false;
            }

            return bulletPrefab.TryValidate(out error);
        }

        public void StartRun(int startedLevelRunId, RoadLayoutSnapshot roadLayout)
        {
            EnsureInitialized();
            if (running)
            {
                throw new InvalidOperationException("BulletManager already has an active run.");
            }

            if (startedLevelRunId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startedLevelRunId));
            }

            if (!IsFinite(roadLayout.TopBoundary))
            {
                throw new ArgumentException("Road top boundary must be finite.", nameof(roadLayout));
            }

            levelRunId = startedLevelRunId;
            topBoundary = roadLayout.TopBoundary;
            nextRuntimeInstanceId = 0;
            activeBullets.Clear();
            pendingRecycles.Clear();
            running = true;
        }

        public void Spawn(BulletSpawnRequest request)
        {
            if (!running || request.LevelRunId != levelRunId)
            {
                return;
            }

            if (request.BulletId < 0 || request.WeaponId < 0 || request.SourceArmyId < 0 ||
                request.SourceSlotIndex < 0 || !IsFinite(request.WorldPosition) ||
                !IsFinite(request.Direction) || request.Direction.sqrMagnitude <= 0f)
            {
                throw new ArgumentException("Bullet spawn request contains invalid values.", nameof(request));
            }

            var config = configProvider.GetBulletConfig(request.BulletId);
            var bullet = pool.RentInactive();
            bullet.InitializeRuntime(request, config, nextRuntimeInstanceId++, transform);
            activeBullets.Add(bullet);
            bullet.gameObject.SetActive(true);
        }

        public void TickMovementAndHits(int tickingLevelRunId, float bulletDeltaTime)
        {
            if (!IsCurrentRun(tickingLevelRunId))
            {
                return;
            }

            if (!IsFinite(bulletDeltaTime) || bulletDeltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(bulletDeltaTime));
            }

            var filter = new ContactFilter2D();
            filter.SetLayerMask(hittableLayers);
            filter.useTriggers = true;

            // 命中回调可能因换武器立即生成新子弹；新实例从下一逻辑帧开始移动，
            // 避免它在本次遍历中继续命中并形成同帧连锁。
            var bulletsToProcess = activeBullets.Count;
            for (var index = 0; index < bulletsToProcess; index++)
            {
                var bullet = activeBullets[index];
                if (bullet == null || !bullet.IsRuntimeActive || pendingRecycles.Contains(bullet))
                {
                    continue;
                }

                var desiredPosition = bullet.GetDesiredPosition(bulletDeltaTime);
                var distance = bullet.GetTravelDistance(desiredPosition);
                if (TryFindHit(bullet, filter, distance, out var candidate))
                {
                    var hitPosition = candidate.Hit.point;
                    if (!IsFinite(hitPosition))
                    {
                        hitPosition = bullet.transform.position;
                    }

                    bullet.ApplyPosition(hitPosition);
                    candidate.Target.ReceiveBulletHit(bullet.CreateDamageContext(hitPosition));
                    pendingRecycles.Add(bullet);
                    continue;
                }

                bullet.ApplyPosition(desiredPosition);
                if (desiredPosition.y > topBoundary)
                {
                    pendingRecycles.Add(bullet);
                }
            }
        }

        public void FlushPendingRecycles(int flushingLevelRunId)
        {
            if (!IsCurrentRun(flushingLevelRunId))
            {
                return;
            }

            for (var index = activeBullets.Count - 1; index >= 0; index--)
            {
                var bullet = activeBullets[index];
                if (bullet == null || !pendingRecycles.Contains(bullet))
                {
                    continue;
                }

                activeBullets.RemoveAt(index);
                pendingRecycles.Remove(bullet);
                bullet.PrepareForPool();
                pool.Return(bullet);
            }
        }

        public void StopRun(int stoppedLevelRunId)
        {
            if (!IsCurrentRun(stoppedLevelRunId))
            {
                return;
            }

            for (var index = activeBullets.Count - 1; index >= 0; index--)
            {
                var bullet = activeBullets[index];
                if (bullet == null)
                {
                    continue;
                }

                bullet.PrepareForPool();
                pool.Return(bullet);
            }

            activeBullets.Clear();
            pendingRecycles.Clear();
            running = false;
            levelRunId = 0;
        }

        private bool TryFindHit(
            Bullet bullet,
            ContactFilter2D filter,
            float distance,
            out HitCandidate bestCandidate)
        {
            bestCandidate = default(HitCandidate);
            candidatesByRuntimeId.Clear();
            if (distance <= 0f)
            {
                return false;
            }

            var hitCount = bullet.BodyCollider.Cast(bullet.Direction, filter, castResults, distance);
            for (var index = 0; index < hitCount; index++)
            {
                var hit = castResults[index];
                if (hit.collider == null ||
                    !hit.collider.TryGetComponent<BulletHitProxy>(out var proxy) ||
                    !proxy.TryGetTarget(out var target) ||
                    target.RuntimeInstanceId < 0 ||
                    !target.CanReceiveBulletHit)
                {
                    continue;
                }

                var candidate = new HitCandidate(target, hit);
                var identityKey = ((long)(int)target.BulletTargetKind << 32) |
                                  (uint)target.RuntimeInstanceId;
                if (!candidatesByRuntimeId.TryGetValue(identityKey, out var existing) ||
                    CompareCandidates(candidate, existing) < 0)
                {
                    candidatesByRuntimeId[identityKey] = candidate;
                }
            }

            var found = false;
            foreach (var candidate in candidatesByRuntimeId.Values)
            {
                if (!found || CompareCandidates(candidate, bestCandidate) < 0)
                {
                    found = true;
                    bestCandidate = candidate;
                }
            }

            return found;
        }

        private static int CompareCandidates(HitCandidate left, HitCandidate right)
        {
            var distanceComparison = left.Hit.distance.CompareTo(right.Hit.distance);
            if (distanceComparison != 0)
            {
                return distanceComparison;
            }

            var kindComparison = left.Target.BulletTargetKind.CompareTo(right.Target.BulletTargetKind);
            return kindComparison != 0
                ? kindComparison
                : left.Target.RuntimeInstanceId.CompareTo(right.Target.RuntimeInstanceId);
        }

        private bool IsCurrentRun(int queriedLevelRunId)
        {
            return running && queriedLevelRunId > 0 && queriedLevelRunId == levelRunId;
        }

        private void EnsureInitialized()
        {
            if (!initialized)
            {
                throw new InvalidOperationException("BulletManager has not been initialized.");
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
        }

        private readonly struct HitCandidate
        {
            public HitCandidate(IRuntimeBulletTarget target, RaycastHit2D hit)
            {
                Target = target;
                Hit = hit;
            }

            public IRuntimeBulletTarget Target { get; }
            public RaycastHit2D Hit { get; }
        }
    }
}
