﻿using System.Numerics;

namespace Walgelijk.ParticleSystem
{
    [RequiresComponents(typeof(TransformComponent))]
    public class ParticlesComponent : Component
    {
        public readonly int MaxParticleCount = 1000;
        public int CurrentParticleCount = 0;

        public readonly Particle[] RawParticleArray;

        public ParticlesComponent(int maxCount = 1000)
        {
            MaxParticleCount = maxCount;
            RawParticleArray = new Particle[MaxParticleCount];

            VertexBuffer = new VertexBuffer(PrimitiveMeshes.CenteredQuad.Vertices, PrimitiveMeshes.CenteredQuad.Indices, new VertexAttributeArray[]{
                new Matrix4x4AttributeArray(new Matrix4x4[MaxParticleCount]), // transform
                new Vector4AttributeArray(new Vector4[MaxParticleCount]), // color
            });

            RenderTask = new InstancedShapeRenderTask(VertexBuffer, material: Material);
        }

        public Material Material = Particle.DefaultMaterial;

        public Vec2Range Gravity = new (new Vector2(0, -9.81f));
        public FloatRange SpawnRadius = new (0.2f, 1f);
        public FloatRange LifeRange = new (1, 3);

        public FloatRange StartSize = new (1);
        public FloatRange StartRotation = new (0);
        public Vec2Range StartVelocity = new (Vector2.One * -4, Vector2.One * 4);
        public FloatRange StartRotationalVelocity = new (-4, 4);
        public ColorRange StartColor = new (Colors.White);
        public FloatRange Dampening = new (0.1f);
        public FloatRange RotationalDampening = new (0.1f);

        public FloatCurve SizeOverLife = new (new Curve<float>.Key(1, 1));
        public ColorCurve ColorOverLife = new (new Curve<Color>.Key(Colors.White, 1));

        public float? FloorLevel = null;
        public float FloorBounceFactor = 0.4f;
        public float FloorCollisionDampeningFactor = 0.4f;
        public Hook<Particle> OnHitFloor = new();

        public float SimulationSpeed = 1;
        public bool WorldSpace;
        public float EmissionRate = 150;
        public bool CircularStartVelocity = false;

        public readonly InstancedShapeRenderTask RenderTask;
        public readonly VertexBuffer VertexBuffer;

        internal FixedIntervalDistributor EmissionDistributor = new();

        public RenderOrder Depth = default;

        public Particle GenerateParticleObject()
        {
            Particle particle = new Particle
            {
                Angle = StartRotation.GetRandom(),
                Position = Utilities.RandomPointInCircle(SpawnRadius.Min, SpawnRadius.Max),
                Velocity = StartVelocity.GetRandom(),
                MaxLife = LifeRange.GetRandom(),
                InitialColor = StartColor.GetRandom(),
                Gravity = Gravity.GetRandom(),
                InitialSize = StartSize.GetRandom(),
                RotationalVelocity = StartRotationalVelocity.GetRandom(),
                Dampening = Dampening.GetRandom(),
                RotationalDampening = RotationalDampening.GetRandom(),

                Life = 0,
            };

            return particle;
        }
    }
}
