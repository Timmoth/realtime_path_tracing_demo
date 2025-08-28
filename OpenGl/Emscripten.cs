﻿using System.Runtime.InteropServices;

namespace realtime_path_tracing_demo.OpenGl;

internal static class Emscripten
{
    [DllImport("emscripten", EntryPoint = "emscripten_request_animation_frame_loop")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern unsafe void RequestAnimationFrameLoop(void* f, nint userDataPtr);
}