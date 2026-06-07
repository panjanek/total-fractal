using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
// Disambiguate from System.Windows.Forms.NativeWindow (pulled in by implicit WinForms usings).
using NativeWindow = OpenTK.Windowing.Desktop.NativeWindow;

namespace TotalFractal.Infrastructure;

/// <summary>
/// Provides a current OpenGL 4.3 context without a visible window, so the
/// <see cref="TotalFractal.Rendering.Renderer"/> can run headless (used by --screenshot).
///
/// Rendering always targets an offscreen FBO and pixels are read back from that FBO, so
/// the window itself is just a context provider - its size and visibility don't affect
/// the result.
/// </summary>
public sealed class OffscreenContext : IDisposable
{
    private readonly NativeWindow _window;

    public OffscreenContext(int width, int height)
    {
        var settings = new NativeWindowSettings
        {
            API = ContextAPI.OpenGL,
            APIVersion = new Version(4, 3),
            Profile = ContextProfile.Core,
            Flags = ContextFlags.ForwardCompatible,
            StartVisible = false,
            ClientSize = new Vector2i(Math.Max(1, width), Math.Max(1, height)),
            Title = "TotalFractal (offscreen)",
        };

        _window = new NativeWindow(settings);
        _window.MakeCurrent();
    }

    public void Dispose() => _window.Dispose();
}
