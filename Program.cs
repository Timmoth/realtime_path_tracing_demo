using System;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using realtime_path_tracing_demo.OpenGl;
using Silk.NET.OpenGLES;

[assembly: SupportedOSPlatform("browser")]

namespace realtime_path_tracing_demo;

public static class Program
{
    public static Uri? BaseAddress { get; internal set; }
    private static RayTracer? Demo { get; set; }

    private static int CanvasWidth { get; set; }
    private static int CanvasHeight { get; set; }

    [UnmanagedCallersOnly]
    public static int Frame(double time, nint userData)
    {
        ArgumentNullException.ThrowIfNull(Demo);

        Demo.Render();

        return 1;
    }

    public static void CanvasResized(int width, int height)
    {
        CanvasWidth = width;
        CanvasHeight = height;
        Demo?.CanvasResized(CanvasWidth, CanvasHeight);
    }

    private static async Task<string> DownloadFile(
        HttpClient client,
        string path)
    {
        var response = await client.GetAsync(new Uri(path, UriKind.Relative));
        if (!response.IsSuccessStatusCode)
            throw new Exception();
        return await response.Content.ReadAsStringAsync();
    }

    public static async Task Main(string[] args)
    {
        Console.WriteLine("Hello from dotnet!");

        var display = EGL.GetDisplay(IntPtr.Zero);
        if (display == IntPtr.Zero)
            throw new Exception("Display was null");

        if (!EGL.Initialize(display, out var major, out var minor))
            throw new Exception("Initialize() returned false.");

        var attributeList = new[]
        {
            EGL.EGL_RED_SIZE, 8,
            EGL.EGL_GREEN_SIZE, 8,
            EGL.EGL_BLUE_SIZE, 8,
            EGL.EGL_DEPTH_SIZE, 24,
            EGL.EGL_STENCIL_SIZE, 8,
            EGL.EGL_SURFACE_TYPE, EGL.EGL_WINDOW_BIT,
            EGL.EGL_RENDERABLE_TYPE, EGL.EGL_OPENGL_ES3_BIT,
            EGL.EGL_SAMPLES, 16, //MSAA, 16 samples
            EGL.EGL_NONE
        };

        var config = IntPtr.Zero;
        var numConfig = IntPtr.Zero;
        if (!EGL.ChooseConfig(display, attributeList, ref config, 1, ref numConfig))
            throw new Exception("ChoseConfig() failed");
        if (numConfig == IntPtr.Zero)
            throw new Exception("ChoseConfig() returned no configs");

        if (!EGL.BindApi(EGL.EGL_OPENGL_ES_API))
            throw new Exception("BindApi() failed");

        var ctxAttribs = new[] { EGL.EGL_CONTEXT_CLIENT_VERSION, 3, EGL.EGL_NONE };
        var context = EGL.CreateContext(display, config, EGL.EGL_NO_CONTEXT, ctxAttribs);
        if (context == IntPtr.Zero)
            throw new Exception("CreateContext() failed");

        // now create the surface
        var surface = EGL.CreateWindowSurface(display, config, IntPtr.Zero, IntPtr.Zero);
        if (surface == IntPtr.Zero)
            throw new Exception("CreateWindowSurface() failed");

        if (!EGL.MakeCurrent(display, surface, surface, context))
            throw new Exception("MakeCurrent() failed");

        //_ = EGL.DestroyContext(display, context);
        //_ = EGL.DestroySurface(display, surface);
        //_ = EGL.Terminate(display);

        TrampolineFuncs.ApplyWorkaroundFixingInvocations();

        var gl = GL.GetApi(EGL.GetProcAddress);

        Interop.Initialize();
        ArgumentNullException.ThrowIfNull(BaseAddress);

        var client = new HttpClient
        {
            BaseAddress = BaseAddress
        };
        var vertexShaderSource = await DownloadFile(client, "Assets/Vert.glsl");
        var fragmentShaderSource = await DownloadFile(client, "Assets/Frag.glsl");

        Demo = new RayTracer(gl, vertexShaderSource, fragmentShaderSource);
        Demo?.CanvasResized(CanvasWidth, CanvasHeight);

        unsafe
        {
            Emscripten.RequestAnimationFrameLoop((delegate* unmanaged<double, nint, int>)&Frame, nint.Zero);
        }
    }

    public static void KeyDown(int code)
    {
    }

    public static void KeyUp(int code)
    {
    }


    public static void MouseMove(float x, float y)
    {
    }

    public static void MouseDown()
    {
    }

    public static void MouseUp()
    {
    }
}