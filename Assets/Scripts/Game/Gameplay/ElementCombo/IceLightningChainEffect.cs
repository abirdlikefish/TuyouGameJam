using System.Collections.Generic;
using Game.Contracts;
using UnityEngine;

namespace Game.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class IceLightningChainEffect : ElementComboEffectBase
    {
        [Header("玩法")]
        [SerializeField, Min(1)] private int effectDamage = 3;
        [SerializeField, Min(0.01f)] private float radius = 2f;
        [SerializeField, Min(1)] private int targetCount = 3;

        [Header("表现")]
        [SerializeField] private LineRenderer glowLine;
        [SerializeField] private LineRenderer coreLine;
        [SerializeField, Min(0.05f)] private float pointSpacing = 0.28f;
        [SerializeField, Min(0f)] private float jitterAmplitude = 0.12f;
        [SerializeField, Min(0.01f)] private float shapeRefreshInterval = 0.04f;
        [SerializeField, Min(0.001f)] private float glowWidth = 0.12f;
        [SerializeField, Min(0.001f)] private float coreWidth = 0.035f;
        [SerializeField] private Color glowColor = new Color(0.25f, 0.55f, 1f, 0.35f);
        [SerializeField] private Color coreColor = new Color(0.85f, 1f, 1f, 1f);

        private readonly List<EnemyEffectTargetSnapshot> candidates =
            new List<EnemyEffectTargetSnapshot>(32);
        private readonly List<EnemyEffectTargetSnapshot> selected =
            new List<EnemyEffectTargetSnapshot>(8);
        private readonly List<Vector3> shapePoints = new List<Vector3>(96);
        private uint visualSeed;
        private int shapeVersion;
        private float refreshRemaining;

        protected override bool ResolveGameplay(
            ElementComboHitRequest request,
            IEnemyEffectService enemyService)
        {
            if (!enemyService.TryGetAliveEffectTarget(
                    request.LevelRunId,
                    request.PrimaryTargetRuntimeId,
                    out var primary))
            {
                return false;
            }

            enemyService.CollectAliveEffectTargets(
                request.LevelRunId,
                request.PrimaryTargetCenter,
                radius,
                candidates);
            for (var index = candidates.Count - 1; index >= 0; index--)
            {
                if (candidates[index].RuntimeInstanceId == primary.RuntimeInstanceId)
                {
                    candidates.RemoveAt(index);
                }
            }

            visualSeed = CreateSeed(
                request.LevelRunId,
                request.DirectDamage.BulletInstanceId,
                ElementComboKind.IceLightningChain);
            var random = new DeterministicRandom(visualSeed);
            // 候选已按运行时 ID 排序；确定性洗牌使同一输入可复现。
            for (var index = candidates.Count - 1; index > 0; index--)
            {
                var swapIndex = random.Next(index + 1);
                var temporary = candidates[index];
                candidates[index] = candidates[swapIndex];
                candidates[swapIndex] = temporary;
            }

            selected.Clear();
            selected.Add(primary);
            var additionalCount = Mathf.Min(targetCount - 1, candidates.Count);
            for (var index = 0; index < additionalCount; index++)
            {
                selected.Add(candidates[index]);
            }

            for (var index = 0; index < selected.Count; index++)
            {
                var target = selected[index];
                var directDamage = target.RuntimeInstanceId == primary.RuntimeInstanceId
                    ? request.DirectDamage.Damage
                    : 0;
                var damage = new EnemyDamageContext(
                    request.DirectDamage,
                    ElementComboKind.IceLightningChain,
                    directDamage,
                    effectDamage,
                    target.Center,
                    request.DirectDamage.HitDirection);
                enemyService.ApplyEnemyDamage(request.LevelRunId, target.RuntimeInstanceId, damage);
            }

            return true;
        }

        protected override void PrepareVisual()
        {
            shapeVersion = 0;
            refreshRemaining = shapeRefreshInterval;
            BuildShape();
        }

        protected override void ShowVisual()
        {
            var visible = selected.Count > 1;
            glowLine.enabled = visible;
            coreLine.enabled = visible;
        }

        protected override void UpdateVisual(float normalizedTime, float deltaTime)
        {
            refreshRemaining -= deltaTime;
            if (selected.Count > 1 && refreshRemaining <= 0f)
            {
                shapeVersion++;
                refreshRemaining += shapeRefreshInterval;
                BuildShape();
            }

            var fade = 1f - normalizedTime;
            glowLine.startColor = WithAlpha(glowColor, fade);
            glowLine.endColor = WithAlpha(glowColor, fade);
            glowLine.startWidth = glowWidth;
            glowLine.endWidth = glowWidth;
            coreLine.startColor = WithAlpha(coreColor, fade);
            coreLine.endColor = WithAlpha(coreColor, fade);
            coreLine.startWidth = coreWidth;
            coreLine.endWidth = coreWidth;
        }

        protected override void HideVisual()
        {
            if (glowLine != null)
            {
                glowLine.enabled = false;
                glowLine.positionCount = 0;
            }

            if (coreLine != null)
            {
                coreLine.enabled = false;
                coreLine.positionCount = 0;
            }
        }

        protected override void ResetRuntime()
        {
            candidates.Clear();
            selected.Clear();
            shapePoints.Clear();
            visualSeed = 0;
            shapeVersion = 0;
            refreshRemaining = 0f;
        }

        public override bool TryValidate(out string error)
        {
            if (!base.TryValidate(out error))
            {
                return false;
            }

            if (effectDamage <= 0 || targetCount <= 0 || !IsFinite(radius) || radius <= 0f ||
                !IsFinite(pointSpacing) || pointSpacing <= 0f ||
                !IsFinite(jitterAmplitude) || jitterAmplitude < 0f ||
                !IsFinite(shapeRefreshInterval) || shapeRefreshInterval <= 0f ||
                !IsFinite(glowWidth) || glowWidth <= 0f ||
                !IsFinite(coreWidth) || coreWidth <= 0f)
            {
                error = $"{name} requires positive gameplay and line-rendering values.";
                return false;
            }

            if (!ValidateLine(glowLine, transform, nameof(glowLine), out error) ||
                !ValidateLine(coreLine, transform, nameof(coreLine), out error) ||
                glowLine == coreLine)
            {
                if (string.IsNullOrEmpty(error))
                {
                    error = $"{name} requires distinct glow and core lines.";
                }

                return false;
            }

            return true;
        }

        private void BuildShape()
        {
            shapePoints.Clear();
            if (selected.Count <= 1)
            {
                glowLine.positionCount = 0;
                coreLine.positionCount = 0;
                return;
            }

            var random = new DeterministicRandom(visualSeed + (uint)(shapeVersion * 7919));
            for (var linkIndex = 0; linkIndex < selected.Count - 1; linkIndex++)
            {
                var start = selected[linkIndex].Center;
                var end = selected[linkIndex + 1].Center;
                var offset = end - start;
                var distance = offset.magnitude;
                var segmentCount = Mathf.Clamp(Mathf.CeilToInt(distance / pointSpacing), 4, 18);
                var direction = distance > 0.0001f ? offset / distance : Vector2.up;
                var perpendicular = new Vector2(-direction.y, direction.x);
                var firstPoint = linkIndex == 0 ? 0 : 1;
                for (var pointIndex = firstPoint; pointIndex <= segmentCount; pointIndex++)
                {
                    var t = (float)pointIndex / segmentCount;
                    var point = Vector2.Lerp(start, end, t);
                    if (pointIndex > 0 && pointIndex < segmentCount)
                    {
                        var envelope = Mathf.Sin(Mathf.PI * t);
                        point += perpendicular * random.NextSigned() * jitterAmplitude * envelope;
                    }

                    shapePoints.Add(new Vector3(point.x, point.y, 0f));
                }
            }

            glowLine.positionCount = shapePoints.Count;
            coreLine.positionCount = shapePoints.Count;
            for (var index = 0; index < shapePoints.Count; index++)
            {
                glowLine.SetPosition(index, shapePoints[index]);
                coreLine.SetPosition(index, shapePoints[index]);
            }
        }

        private static uint CreateSeed(int levelRunId, int bulletInstanceId, ElementComboKind comboKind)
        {
            unchecked
            {
                var seed = 2166136261u;
                seed = (seed ^ (uint)levelRunId) * 16777619u;
                seed = (seed ^ (uint)bulletInstanceId) * 16777619u;
                seed = (seed ^ (uint)comboKind) * 16777619u;
                return seed == 0u ? 1u : seed;
            }
        }

        private struct DeterministicRandom
        {
            private uint state;

            public DeterministicRandom(uint seed)
            {
                state = seed == 0u ? 1u : seed;
            }

            public int Next(int exclusiveMaximum)
            {
                return exclusiveMaximum <= 1 ? 0 : (int)(NextUInt() % (uint)exclusiveMaximum);
            }

            public float NextSigned()
            {
                return NextUInt() / (float)uint.MaxValue * 2f - 1f;
            }

            private uint NextUInt()
            {
                var value = state;
                value ^= value << 13;
                value ^= value >> 17;
                value ^= value << 5;
                state = value == 0u ? 1u : value;
                return state;
            }
        }
    }
}
