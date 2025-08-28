using System;
using System.Numerics;

namespace realtime_path_tracing_demo;

public class PhysicsSimulation
{
    public const int MaxSpheres = 16;
    private readonly SphereState[] _spheres = new SphereState[MaxSpheres];
    public readonly int NumActiveSpheres = 8;

    public void InitSpheres()
    {
        var rng = new Random(1234);
        for (var i = 0; i < NumActiveSpheres; i++)
        {
            _spheres[i].Radius = 0.1f + (float)rng.NextDouble() * 0.5f;
            _spheres[i].Position = new Vector3(
                (float)(rng.NextDouble() * 8 - 4), // spread X
                (float)rng.NextDouble() * 2, // drop from height
                (float)(rng.NextDouble() * 8 - 4) // spread Z
            );
            _spheres[i].Velocity = new Vector3(
                rng.NextSingle() - 0.5f,
                rng.NextSingle() - 0.5f,
                rng.NextSingle() - 0.5f
            );
            _spheres[i].Acceleration = Vector3.Zero; // initialize acceleration
            _spheres[i].Color = new Vector4(1f, 1f, 1f, 1f);
            _spheres[i].Roughness = 0.01f * (float)rng.NextDouble();
            _spheres[i].Metallic = 0.95f;
        }
    }

    public void UpdatePhysics(float dt, Vector3 cameraPosition)
    {
        for (var i = 0; i < NumActiveSpheres; i++)
        {
            // Reset acceleration
            _spheres[i].Acceleration = Vector3.Zero;

            // Gravity
            _spheres[i].Acceleration += new Vector3(0, -9.81f, 0);

            // Mass proportional to volume
            _spheres[i].Mass = MathF.Pow(_spheres[i].Radius, 3);

            // Integrate velocity and position
            _spheres[i].Velocity += _spheres[i].Acceleration * dt;
            _spheres[i].Position += _spheres[i].Velocity * dt;

            // --- Floor / Ceiling collisions ---
            var restitution = 0.9f;
            var friction = 0.99f;
            float minY = -0.5f, maxY = 4f;

            if (_spheres[i].Position.Y - _spheres[i].Radius < minY)
            {
                _spheres[i].Position.Y = minY + _spheres[i].Radius;
                _spheres[i].Velocity.Y *= -restitution;
                _spheres[i].Velocity.X *= friction;
                _spheres[i].Velocity.Z *= friction;
            }
            else if (_spheres[i].Position.Y + _spheres[i].Radius > maxY)
            {
                _spheres[i].Position.Y = maxY - _spheres[i].Radius;
                _spheres[i].Velocity.Y *= -restitution;
                _spheres[i].Velocity.X *= friction;
                _spheres[i].Velocity.Z *= friction;
            }

            // --- Cylinder walls ---
            var cylRadius = 4f;
            var posXZ = new Vector2(_spheres[i].Position.X, _spheres[i].Position.Z);
            var distXZ = posXZ.Length();
            var maxDist = cylRadius - _spheres[i].Radius;
            if (distXZ > maxDist)
            {
                var normalXZ = posXZ / distXZ;
                posXZ = normalXZ * maxDist;
                _spheres[i].Position.X = posXZ.X;
                _spheres[i].Position.Z = posXZ.Y;

                var vel = _spheres[i].Velocity;
                var vDotN = vel.X * normalXZ.X + vel.Z * normalXZ.Y;
                if (!(vDotN > 0)) continue;
                var normal3 = new Vector3(normalXZ.X, 0, normalXZ.Y);
                _spheres[i].Velocity -= (1f + restitution) * vDotN * normal3;
                _spheres[i].Velocity *= friction;
            }
        }

        // --- Sphere-sphere collisions ---
        for (var i = 0; i < NumActiveSpheres; i++)
        for (var j = i + 1; j < NumActiveSpheres; j++)
        {
            var delta = _spheres[j].Position - _spheres[i].Position;
            var dist = delta.Length();
            var minDist = _spheres[i].Radius + _spheres[j].Radius;
            if (!(dist < minDist) || !(dist > 1e-6f)) continue;
            var normal = delta / dist;
            var penetration = minDist - dist;
            var m1 = _spheres[i].Mass;
            var m2 = _spheres[j].Mass;
            var totalMass = m1 + m2;

            _spheres[i].Position -= normal * (penetration * (m2 / totalMass));
            _spheres[j].Position += normal * (penetration * (m1 / totalMass));

            var relVel = _spheres[j].Velocity - _spheres[i].Velocity;
            var velAlongNormal = Vector3.Dot(relVel, normal);
            if (!(velAlongNormal <= 0)) continue;
            var jImpulse = -(1f + 0.9f) * velAlongNormal / (1f / m1 + 1f / m2);
            var impulse = jImpulse * normal;
            _spheres[i].Velocity -= impulse / m1;
            _spheres[j].Velocity += impulse / m2;

            // Tangential friction
            var tangent = relVel - velAlongNormal * normal;
            if (!(tangent.LengthSquared() > 1e-6f)) continue;
            tangent = Vector3.Normalize(tangent);
            var jt = -Vector3.Dot(relVel, tangent);
            jt /= 1f / m1 + 1f / m2;
            var frictionImpulse = jt * tangent * (1f - 0.9f);
            _spheres[i].Velocity -= frictionImpulse / m1;
            _spheres[j].Velocity += frictionImpulse / m2;
        }

        // --- Camera collision (sphere) ---
        var camCenter = cameraPosition;
        var camRadius = 0.5f;
        for (var i = 0; i < NumActiveSpheres; i++)
        {
            var delta = _spheres[i].Position - camCenter;
            var dist = delta.Length();
            var minDist = _spheres[i].Radius + camRadius;
            if (!(dist < minDist) || !(dist > 1e-6f)) continue;
            var normal = delta / dist;
            var penetration = minDist - dist;
            _spheres[i].Position += normal * penetration;

            var velAlongNormal = Vector3.Dot(_spheres[i].Velocity, normal);
            if (!(velAlongNormal < 0)) continue;
            _spheres[i].Velocity -= (1f + 0.9f) * velAlongNormal * normal;
            _spheres[i].Velocity *= 0.9f;
        }
    }

    public void Flatten(Span<float> sphereData)
    {
        for (var i = 0; i < NumActiveSpheres; i++)
        {
            var s = _spheres[i];
            var o = i * 16;
            sphereData[o + 0] = s.Position.X;
            sphereData[o + 1] = s.Position.Y;
            sphereData[o + 2] = s.Position.Z;
            sphereData[o + 3] = s.Radius;
            sphereData[o + 4] = s.Color.X;
            sphereData[o + 5] = s.Color.Y;
            sphereData[o + 6] = s.Color.Z;
            sphereData[o + 7] = s.Color.W;
            sphereData[o + 8] = 0;
            sphereData[o + 9] = 0;
            sphereData[o + 10] = 0;
            sphereData[o + 11] = 0;
            sphereData[o + 12] = 0;
            sphereData[o + 13] = s.Roughness;
            sphereData[o + 14] = s.Metallic;
            sphereData[o + 15] = 0;
        }
    }

    private struct SphereState
    {
        public Vector3 Position; // Current position
        public Vector3 Velocity; // Current velocity
        public Vector3 Acceleration; // Accumulated force / acceleration
        public float Radius;
        public Vector4 Color;
        public float Roughness;
        public float Metallic;
        public float Mass { get; set; }
    }
}