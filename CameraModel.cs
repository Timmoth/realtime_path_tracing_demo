using System;
using System.Numerics;

namespace realtime_path_tracing_demo;

public class CameraModel
{
    private float _cameraSpeed = 0.1f; // units per second
    public Vector3 CameraDirection = new(0, 0, 1); // start just above origin
    public Vector3 CameraPosition = new(0, 0, 3); // start just above origin

    public void UpdateCamera(float time)
    {
        // The point the camera is circling
        var target = Vector3.Zero;

        // Helicopter orbit settings
        var orbitRadius = 1.0f; // how far out from target
        var orbitHeight = .5f; // how high above target
        var orbitSpeed = 0.025f; // radians per second

        // Compute orbit angle from time
        var angle = time * 0.001f * orbitSpeed;

        // Update camera position in a circle around target
        CameraPosition = new Vector3(
            target.X + orbitRadius * MathF.Cos(angle),
            target.Y + orbitHeight,
            target.Z + orbitRadius * MathF.Sin(angle)
        );

        // Always look at the target
        CameraDirection = Vector3.Normalize(target - CameraPosition);
    }
}