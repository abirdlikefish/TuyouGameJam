using Game.Contracts;
using UnityEngine;

namespace Game.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class FireIceSteamEffect : ElementComboEffectBase
    {
        [Header("玩法")]
        [SerializeField, Min(0.01f)] private float pushDistance = 1f;

        [Header("表现")]
        [SerializeField] private LineRenderer steamLine;
        [SerializeField, Min(0.001f)] private float startWidth = 0.28f;
        [SerializeField, Min(0.001f)] private float endWidth = 0.08f;
        [SerializeField] private Color steamColor = new Color(0.85f, 0.9f, 0.95f, 0.75f);

        private Vector2 startPosition;
        private Vector2 endPosition;

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

            startPosition = primary.Center;
            endPosition = startPosition + Vector2.up * pushDistance;
            // 先登记位移，LevelManager 会在所有子弹结算后统一应用并同步物理姿态。
            enemyService.QueueEnemyDisplacement(
                request.LevelRunId,
                primary.RuntimeInstanceId,
                Vector2.up * pushDistance);
            var damage = new EnemyDamageContext(
                request.DirectDamage,
                ElementComboKind.FireIceSteam,
                request.DirectDamage.Damage,
                0,
                primary.Center,
                request.DirectDamage.HitDirection);
            enemyService.ApplyEnemyDamage(request.LevelRunId, primary.RuntimeInstanceId, damage);
            return true;
        }

        protected override void PrepareVisual()
        {
            const int PointCount = 7;
            steamLine.positionCount = PointCount;
            for (var index = 0; index < PointCount; index++)
            {
                var t = (float)index / (PointCount - 1);
                var point = Vector2.Lerp(startPosition, endPosition, t);
                point.x += Mathf.Sin(t * Mathf.PI * 3f) * 0.08f * Mathf.Sin(Mathf.PI * t);
                steamLine.SetPosition(index, new Vector3(point.x, point.y, 0f));
            }
        }

        protected override void ShowVisual()
        {
            steamLine.enabled = true;
        }

        protected override void UpdateVisual(float normalizedTime, float deltaTime)
        {
            var fade = 1f - normalizedTime;
            steamLine.startColor = WithAlpha(steamColor, fade);
            steamLine.endColor = WithAlpha(steamColor, fade * 0.25f);
            steamLine.startWidth = startWidth * (1f + normalizedTime * 0.4f);
            steamLine.endWidth = endWidth * (1f + normalizedTime * 0.4f);
        }

        protected override void HideVisual()
        {
            if (steamLine != null)
            {
                steamLine.enabled = false;
                steamLine.positionCount = 0;
            }
        }

        protected override void ResetRuntime()
        {
            startPosition = Vector2.zero;
            endPosition = Vector2.zero;
        }

        public override bool TryValidate(out string error)
        {
            if (!base.TryValidate(out error))
            {
                return false;
            }

            if (!IsFinite(pushDistance) || pushDistance <= 0f ||
                !IsFinite(startWidth) || startWidth <= 0f ||
                !IsFinite(endWidth) || endWidth <= 0f)
            {
                error = $"{name} requires positive push and line-rendering values.";
                return false;
            }

            return ValidateLine(steamLine, transform, nameof(steamLine), out error);
        }
    }
}
