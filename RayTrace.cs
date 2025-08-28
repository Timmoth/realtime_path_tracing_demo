using System;
using System.Diagnostics;
using Silk.NET.OpenGLES;

namespace realtime_path_tracing_demo;

public class RayTracer
{
    private readonly CameraModel _cameraModel = new();
    private readonly PhysicsSimulation _physics = new();
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private readonly Stopwatch _total = Stopwatch.StartNew();
    private int _height;
    private int _width;

    public unsafe RayTracer(GL gl, string rtVertexSource, string rtFragmentSource)
    {
        Gl = gl;

        // Compile Ray Tracing Shader
        ShaderProgram = Gl.CreateProgram();
        var rtVertexShader = CompileShader(ShaderType.VertexShader, rtVertexSource);
        var rtFragmentShader = CompileShader(ShaderType.FragmentShader, rtFragmentSource);
        Gl.AttachShader(ShaderProgram, rtVertexShader);
        Gl.AttachShader(ShaderProgram, rtFragmentShader);
        Gl.LinkProgram(ShaderProgram);
        Gl.GetProgram(ShaderProgram, ProgramPropertyARB.LinkStatus, out var linkStatus);
        Debug.Assert(linkStatus != 0, "Ray Tracing shader program failed to link.");
        Gl.DeleteShader(rtVertexShader);
        Gl.DeleteShader(rtFragmentShader);

        ResolutionLocation = Gl.GetUniformLocation(ShaderProgram, "u_resolution");
        NumSpheresLocation = Gl.GetUniformLocation(ShaderProgram, "u_num_spheres");
        FrameLocationRt = Gl.GetUniformLocation(ShaderProgram, "u_frame");
        CameraPositionLocation = Gl.GetUniformLocation(ShaderProgram, "u_camPos");
        CameraDirectionLocation = Gl.GetUniformLocation(ShaderProgram, "u_camDir");

        // Fullscreen quad
        float[] quadVertices = { -1f, 1f, -1f, -1f, 1f, -1f, 1f, 1f };
        ushort[] quadIndices = { 0, 1, 2, 0, 2, 3 };

        VAO = Gl.GenVertexArray();
        Gl.BindVertexArray(VAO);

        var vbo = Gl.GenBuffer();
        Gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        Gl.BufferData<float>(BufferTargetARB.ArrayBuffer, quadVertices, BufferUsageARB.StaticDraw);

        var ebo = Gl.GenBuffer();
        Gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, ebo);
        Gl.BufferData<ushort>(BufferTargetARB.ElementArrayBuffer, quadIndices, BufferUsageARB.StaticDraw);

        Gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), null);
        Gl.EnableVertexAttribArray(0);
        Gl.BindVertexArray(0);

        // UBO for sphere data
        SphereUBO = Gl.GenBuffer();
        Gl.BindBuffer(BufferTargetARB.UniformBuffer, SphereUBO);
        Gl.BufferData(BufferTargetARB.UniformBuffer,
            PhysicsSimulation.MaxSpheres * 16 * sizeof(float), null, BufferUsageARB.DynamicDraw);
        Gl.BindBufferBase(BufferTargetARB.UniformBuffer, 0, SphereUBO);
        var blockIndex = Gl.GetUniformBlockIndex(ShaderProgram, "SpheresUBO");
        Gl.UniformBlockBinding(ShaderProgram, blockIndex, 0);

        _physics.InitSpheres();
    }

    private GL Gl { get; }

    // Ray Tracing Shader
    private uint ShaderProgram { get; }
    private int ResolutionLocation { get; }
    private int NumSpheresLocation { get; }
    private int FrameLocationRt { get; }
    private int CameraPositionLocation { get; }
    private int CameraDirectionLocation { get; }

    private uint VAO { get; }
    private uint SphereUBO { get; }

    private uint CompileShader(ShaderType type, string source)
    {
        var shader = Gl.CreateShader(type);
        Gl.ShaderSource(shader, source);
        Gl.CompileShader(shader);
        Gl.GetShader(shader, ShaderParameterName.CompileStatus, out var compileStatus);
        if (compileStatus == 0)
        {
            var log = Gl.GetShaderInfoLog(shader);
            throw new Exception($"Shader compilation failed for {type}: {log}");
        }

        return shader;
    }

    public unsafe void Render()
    {
        var dt = _stopwatch.ElapsedMilliseconds;
        _stopwatch.Restart();

        _cameraModel.UpdateCamera(_total.ElapsedMilliseconds);
        _physics.UpdatePhysics(dt / 1000.0f, _cameraModel.CameraPosition);

        // Upload sphere data
        Span<float> sphereData = stackalloc float[PhysicsSimulation.MaxSpheres * 16];
        _physics.Flatten(sphereData);
        Gl.BindBuffer(BufferTargetARB.UniformBuffer, SphereUBO);
        Gl.BufferSubData<float>(BufferTargetARB.UniformBuffer, IntPtr.Zero,
            (nuint)(sphereData.Length * sizeof(float)), sphereData);

        // Render
        Gl.Viewport(0, 0, (uint)_width, (uint)_height);
        Gl.Clear(ClearBufferMask.ColorBufferBit);

        Gl.UseProgram(ShaderProgram);
        Gl.Uniform1(NumSpheresLocation, _physics.NumActiveSpheres);
        Gl.Uniform1(FrameLocationRt, Random.Shared.Next());

        Gl.Uniform3(CameraPositionLocation, _cameraModel.CameraPosition);
        Gl.Uniform3(CameraDirectionLocation, _cameraModel.CameraDirection);

        Gl.BindVertexArray(VAO);
        Gl.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedShort, null);
        Gl.BindVertexArray(0);
    }

    public void CanvasResized(int width, int height)
    {
        _width = width;
        _height = height;

        Gl.Viewport(0, 0, (uint)width, (uint)height);

        Gl.UseProgram(ShaderProgram);
        Gl.Uniform2(ResolutionLocation, width, (float)height);
    }
}