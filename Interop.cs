using System;
using System.Runtime.InteropServices.JavaScript;

namespace realtime_path_tracing_demo;

internal static partial class Interop
{
    [JSImport("initialize", "main.js")]
    public static partial void Initialize();

    [JSExport]
    public static void OnKeyDown(bool shift, bool ctrl, bool alt, bool repeat, int code)
    {
        Program.KeyDown(code);
    }

    [JSExport]
    public static void OnKeyUp(bool shift, bool ctrl, bool alt, int code)
    {
        Program.KeyUp(code);
    }

    [JSExport]
    public static void OnMouseMove(float x, float y)
    {
        Program.MouseMove(x, y);
    }

    [JSExport]
    public static void OnMouseDown(bool shift, bool ctrl, bool alt, int button)
    {
        Program.MouseDown();
    }

    [JSExport]
    public static void OnMouseUp(bool shift, bool ctrl, bool alt, int button)
    {
        Program.MouseUp();
    }

    [JSExport]
    public static void OnCanvasResize(float width, float height, float devicePixelRatio)
    {
        Program.CanvasResized((int)width, (int)height);
    }

    [JSExport]
    public static void SetRootUri(string uri)
    {
        Program.BaseAddress = new Uri(uri);
    }

    [JSExport]
    public static void AddLocale(string locale)
    {
    }
}