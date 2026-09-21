using System.Collections.Generic;
using Game.Contracts;
using UnityEngine;

namespace Game.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class FireLightningExplosionEffect : ElementComboEffectBase
    {
        [Header("玩法")]
        [SerializeField, Min(1)] private int effectDamage = 3;
        [SerializeField, Min(0.01f)] private float radius = 1.5f;

        [Header("表现")]
        [SerializeField] private LineRenderer glowLine;
        [SerializeField] private LineRenderer coreLine;
        [SerializeField, Range(12, 64)] private int ringSegments = 32;
        [SerializeField, Min(0.001f)] private float glowWidth = 0.14f;
        [SerializeField, Min(0.001f)] private float coreWidth = 0.045f;
        [SerializeField] private Color glowColor = new Color(1f, 0.35f, 0.05f, 0.35f);
        [SerializeField] private Color coreColor = new Color(1f, 0.9f, 0.4f, 0.95f);

        private readonly List<EnemyEffectTargetSnapshot> targets =
            new List<EnemyEffectTargetSnapshot>(32);
        private Vector2 center;

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

            center = request.PrimaryTargetCenter;
            enemyService.CollectAliveEffectTargets(request.LevelRunId, center, radius, targets);
            var foundPrimary = false;
            for (var index = 0; index < targets.Count; index++)
            {
                if (targets[index].RuntimeInstanceId == primary.RuntimeInstanceId)
                {
                    foundPrimary = true;
                    break;
                }
            }

            if (!foundPrimary)
            {
                targets.Add(primary);
                targets.Sort(CompareTargets);
            }

            for (var index = 0; index < targets.Count; index++)
            {
                var target = targets[index];
                var directDamage = target.RuntimeInstanceId == primary.RuntimeInstanceId
                    ? request.DirectDamage.Damage
                    : 0;
                var damage = new EnemyDamageContext(
                    request.DirectDamage,
                    ElementComboKind.FireLightningExplosion,
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
            ConfigureRing(glowLine);
            ConfigureRing(coreLine);
        }

        protected override void ShowVisual()
        {
            glowLine.enabled = true;
            coreLine.enabled = true;
        }

        protected override void UpdateVisual(float normalizedTime, float deltaTime)
        {
            var fade = 1f - normalizedTime;
            var pulse = 1f + Mathf.Sin(normalizedTime * Mathf.PI) * 0.2f;
            glowLine.startColor = WithAlpha(glowColor, fade);
            glowLine.endColor = WithAlpha(glowColor, fade);
            glowLine.startWidth = glowWidth * pulse;
            glowLine.endWidth = glowWidth * pulse;
            coreLine.startColor = WithAlpha(coreColor, fade);
            coreLine.endColor = WithAlpha(coreColor, fade);
            coreLine.startWidth = coreWidth * pulse;
            coreLine.endWidth = coreWidth * pulse;
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
            targets.Clear();
            center = Vector2.zero;
        }

        public override bool TryValidate(out string error)
        {
            if (!base.TryValidate(out error))
            {
                return false;
            }

            if (effectDamage <= 0 || !IsFinite(radius) || radius <= 0f ||
                ringSegments < 12 || !IsFinite(glowWidth) || glowWidth <= 0f ||
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

        private void ConfigureRing(LineRenderer line)
        {
            var count = ringSegments + 1;
            line.positionCount = count;
            for (var index = 0; index < count; index++)
            {
                var angle = (float)index / ringSegments * Mathf.PI * 2f;
                line.SetPosition(
                    index,
                    new Vector3(
                        center.x + Mathf.Cos(angle) * radius,
                        center.y + Mathf.Sin(angle) * radius,
                        0f));
            }
        }

        private static int CompareTargets(
            EnemyEffectTargetSnapshot left,
            EnemyEffectTargetSnapshot right)
        {
            return left.RuntimeInstanceId.CompareTo(right.RuntimeInstanceId);
        }
    }
}
