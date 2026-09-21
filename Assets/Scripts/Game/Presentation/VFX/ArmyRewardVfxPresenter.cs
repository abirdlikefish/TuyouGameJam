using System;
using Game.Contracts;
using UnityEngine;

namespace Game.Presentation
{
    [DisallowMultipleComponent]
    public sealed class ArmyRewardVfxPresenter : MonoBehaviour
    {
        private const int SparkCountPerSlot = 5;
        private const float Tau = Mathf.PI * 2f;

        private static readonly Color32 ArmyAdditionColor = new Color32(82, 255, 174, 255);
        private static readonly Color32 WeaponPickupColor = new Color32(255, 218, 96, 255);
        private static readonly Color32 FireElementColor = new Color32(255, 96, 42, 255);
        private static readonly Color32 IceElementColor = new Color32(78, 218, 255, 255);
        private static readonly Color32 LightningElementColor = new Color32(196, 120, 255, 255);

        [Header("共享粒子")]
        [SerializeField] private ParticleSystem glowParticles;
        [SerializeField] private ParticleSystem ringParticles;
        [SerializeField] private ParticleSystem sparkParticles;

        private IArmyController armyController;
        private IEventBus eventBus;
        private ITimeService timeService;
        private Transform armySimulationSpace;
        private SubscriptionToken gateContactSubscription;
        private SubscriptionToken propBrokenSubscription;
        private SubscriptionToken gooseCageBrokenSubscription;
        private int levelRunId;
        private bool initialized;

        public void Initialize(
            int initializedLevelRunId,
            IArmyController initializedArmyController,
            IEventBus initializedEventBus,
            ITimeService initializedTimeService,
            Transform initializedArmySimulationSpace)
        {
            if (initialized)
            {
                throw new InvalidOperationException("ArmyRewardVfxPresenter is already initialized.");
            }

            if (initializedLevelRunId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initializedLevelRunId));
            }

            armyController = initializedArmyController ??
                throw new ArgumentNullException(nameof(initializedArmyController));
            eventBus = initializedEventBus ??
                throw new ArgumentNullException(nameof(initializedEventBus));
            timeService = initializedTimeService ??
                throw new ArgumentNullException(nameof(initializedTimeService));
            armySimulationSpace = initializedArmySimulationSpace != null
                ? initializedArmySimulationSpace
                : throw new ArgumentNullException(nameof(initializedArmySimulationSpace));

            if (!TryValidate(out var error))
            {
                throw new InvalidOperationException(error);
            }

            levelRunId = initializedLevelRunId;
            ConfigureSimulationSpace(glowParticles);
            ConfigureSimulationSpace(ringParticles);
            ConfigureSimulationSpace(sparkParticles);
            ClearParticles();
            PrimeManualSimulation(glowParticles);
            PrimeManualSimulation(ringParticles);
            PrimeManualSimulation(sparkParticles);

            gateContactSubscription = eventBus.Subscribe<GateContactResolved>(OnGateContactResolved);
            propBrokenSubscription = eventBus.Subscribe<PropBroken>(OnPropBroken);
            gooseCageBrokenSubscription = eventBus.Subscribe<GooseCageBroken>(OnGooseCageBroken);
            initialized = true;
        }

        public bool TryValidate(out string error)
        {
            if (glowParticles == null || ringParticles == null || sparkParticles == null)
            {
                error = $"{name} requires Glow, Ring and Spark ParticleSystem references.";
                return false;
            }

            if (glowParticles == ringParticles ||
                glowParticles == sparkParticles ||
                ringParticles == sparkParticles)
            {
                error = $"{name} requires three distinct ParticleSystem references.";
                return false;
            }

            if (!ValidateParticleSystem(glowParticles, "glowParticles", out error) ||
                !ValidateParticleSystem(ringParticles, "ringParticles", out error) ||
                !ValidateParticleSystem(sparkParticles, "sparkParticles", out error))
            {
                return false;
            }

            error = string.Empty;
            return true;
        }

        public void Cleanup()
        {
            if (eventBus != null)
            {
                Unsubscribe(gateContactSubscription);
                Unsubscribe(propBrokenSubscription);
                Unsubscribe(gooseCageBrokenSubscription);
            }

            gateContactSubscription = default(SubscriptionToken);
            propBrokenSubscription = default(SubscriptionToken);
            gooseCageBrokenSubscription = default(SubscriptionToken);
            ClearParticles();
            ResetSimulationSpace(glowParticles);
            ResetSimulationSpace(ringParticles);
            ResetSimulationSpace(sparkParticles);

            armyController = null;
            eventBus = null;
            timeService = null;
            armySimulationSpace = null;
            levelRunId = 0;
            initialized = false;
        }

        private void Update()
        {
            if (!initialized)
            {
                return;
            }

            var deltaTime = timeService.GetDeltaTime(TimeDomain.VFX);
            if (!IsFinite(deltaTime) || deltaTime < 0f)
            {
                throw new InvalidOperationException("Army reward VFX delta time must be finite and non-negative.");
            }

            SimulateIfAlive(glowParticles, deltaTime);
            SimulateIfAlive(ringParticles, deltaTime);
            SimulateIfAlive(sparkParticles, deltaTime);
        }

        private void OnGateContactResolved(GateContactResolved message)
        {
            if (!initialized || message.LevelRunId != levelRunId || !message.Succeeded || message.RewardLocked)
            {
                return;
            }

            if (message.GateType == GateType.Additive &&
                message.AdditionResult.HasValue &&
                message.AdditionResult.Value.ActualAddition > 0)
            {
                EmitAtActiveSlots(ArmyAdditionColor);
                return;
            }

            if (message.GateType != GateType.Element ||
                !message.ElementDurationChangeResult.HasValue ||
                message.ElementDurationChangeResult.Value.CurrentDuration <=
                message.ElementDurationChangeResult.Value.PreviousDuration)
            {
                return;
            }

            EmitAtActiveSlots(GetElementColor(message.ElementType));
        }

        private void OnPropBroken(PropBroken message)
        {
            if (!initialized ||
                message.LevelRunId != levelRunId ||
                !message.EffectApplied ||
                message.ContactState != PropContactState.Succeeded)
            {
                return;
            }

            EmitAtActiveSlots(WeaponPickupColor);
        }

        private void OnGooseCageBroken(GooseCageBroken message)
        {
            if (!initialized ||
                message.LevelRunId != levelRunId ||
                message.AdditionResult.ActualAddition <= 0)
            {
                return;
            }

            EmitAtActiveSlots(ArmyAdditionColor);
        }

        private void EmitAtActiveSlots(Color32 color)
        {
            var capacity = armyController.GetSlotCapacity();
            for (var slotIndex = 0; slotIndex < capacity; slotIndex++)
            {
                if (!armyController.TryGetSlotTarget(slotIndex, out var target))
                {
                    continue;
                }

                var worldPosition = new Vector3(target.WorldPosition.x, target.WorldPosition.y + 0.1f, 0f);
                var localPosition = armySimulationSpace.InverseTransformPoint(worldPosition);
                EmitGlow(localPosition, color, slotIndex);
                EmitRing(localPosition, color, slotIndex);
                EmitSparks(localPosition, color, slotIndex);
            }
        }

        private void EmitGlow(Vector3 position, Color32 color, int slotIndex)
        {
            var emit = new ParticleSystem.EmitParams
            {
                applyShapeToPosition = false,
                position = position,
                velocity = Vector3.zero,
                startColor = WithAlpha(color, 220),
                startLifetime = 0.34f,
                startSize = 0.9f,
                rotation = GetDeterministicRotation(slotIndex, 0),
                randomSeed = GetDeterministicSeed(slotIndex, 0)
            };
            glowParticles.Emit(emit, 1);
        }

        private void EmitRing(Vector3 position, Color32 color, int slotIndex)
        {
            var emit = new ParticleSystem.EmitParams
            {
                applyShapeToPosition = false,
                position = position,
                velocity = Vector3.zero,
                startColor = WithAlpha(color, 190),
                startLifetime = 0.42f,
                startSize = 0.62f,
                rotation = GetDeterministicRotation(slotIndex, 1),
                randomSeed = GetDeterministicSeed(slotIndex, 1)
            };
            ringParticles.Emit(emit, 1);
        }

        private void EmitSparks(Vector3 position, Color32 color, int slotIndex)
        {
            for (var sparkIndex = 0; sparkIndex < SparkCountPerSlot; sparkIndex++)
            {
                var phase = (sparkIndex + GetSlotPhase(slotIndex)) / SparkCountPerSlot;
                var angle = phase * Tau;
                var speed = 0.72f + 0.09f * ((slotIndex + sparkIndex) % 4);
                var velocity = new Vector3(
                    Mathf.Cos(angle) * speed,
                    Mathf.Sin(angle) * speed + 0.28f,
                    0f);
                var emit = new ParticleSystem.EmitParams
                {
                    applyShapeToPosition = false,
                    position = position,
                    velocity = velocity,
                    startColor = WithAlpha(color, 235),
                    startLifetime = 0.38f + 0.035f * (sparkIndex % 3),
                    startSize = 0.075f + 0.012f * ((slotIndex + sparkIndex) % 3),
                    rotation = angle * Mathf.Rad2Deg,
                    randomSeed = GetDeterministicSeed(slotIndex, sparkIndex + 2)
                };
                sparkParticles.Emit(emit, 1);
            }
        }

        private void ConfigureSimulationSpace(ParticleSystem particleSystem)
        {
            var main = particleSystem.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Custom;
            main.customSimulationSpace = armySimulationSpace;
        }

        private static void ResetSimulationSpace(ParticleSystem particleSystem)
        {
            if (particleSystem == null)
            {
                return;
            }

            var main = particleSystem.main;
            main.customSimulationSpace = null;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
        }

        private static bool ValidateParticleSystem(
            ParticleSystem particleSystem,
            string fieldName,
            out string error)
        {
            var main = particleSystem.main;
            if (main.playOnAwake || main.loop)
            {
                error = $"{particleSystem.name} assigned to {fieldName} must disable Play On Awake and Loop.";
                return false;
            }

            if (particleSystem.emission.enabled)
            {
                error = $"{particleSystem.name} assigned to {fieldName} must disable automatic Emission.";
                return false;
            }

            if (!particleSystem.TryGetComponent<ParticleSystemRenderer>(out var renderer) ||
                renderer.sharedMaterial == null)
            {
                error = $"{particleSystem.name} assigned to {fieldName} requires a material-bound renderer.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static void SimulateIfAlive(ParticleSystem particleSystem, float deltaTime)
        {
            if (particleSystem.particleCount > 0 && deltaTime > 0f)
            {
                particleSystem.Simulate(deltaTime, false, false, false);
            }
        }

        private static void PrimeManualSimulation(ParticleSystem particleSystem)
        {
            // Stopped ParticleSystem 会忽略手动 Emit；先播放再暂停，使它只接受显式 Emit/Simulate。
            particleSystem.Play(false);
            particleSystem.Pause(false);
        }

        private void ClearParticles()
        {
            StopAndClear(glowParticles);
            StopAndClear(ringParticles);
            StopAndClear(sparkParticles);
        }

        private static void StopAndClear(ParticleSystem particleSystem)
        {
            if (particleSystem != null)
            {
                particleSystem.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void Unsubscribe(SubscriptionToken token)
        {
            if (token.IsValid)
            {
                eventBus.Unsubscribe(token);
            }
        }

        private static Color32 GetElementColor(ElementType elementType)
        {
            switch (elementType)
            {
                case ElementType.Fire:
                    return FireElementColor;
                case ElementType.Ice:
                    return IceElementColor;
                case ElementType.Lightning:
                    return LightningElementColor;
                default:
                    throw new ArgumentOutOfRangeException(nameof(elementType), elementType, "Unknown reward element.");
            }
        }

        private static Color32 WithAlpha(Color32 color, byte alpha)
        {
            color.a = alpha;
            return color;
        }

        private static float GetSlotPhase(int slotIndex)
        {
            return (slotIndex * 0.6180339f) % 1f;
        }

        private static float GetDeterministicRotation(int slotIndex, int salt)
        {
            return (slotIndex * 47f + salt * 113f) % 360f;
        }

        private static uint GetDeterministicSeed(int slotIndex, int salt)
        {
            return (uint)(slotIndex * 397 + salt * 31 + 1);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private void OnDestroy()
        {
            Cleanup();
        }
    }
}
