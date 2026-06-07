using System.Windows;
using TotalFractal.Infrastructure;
using TotalFractal.Rendering;
using Application = System.Windows.Application;

namespace TotalFractal;

/// <summary>
/// Interaction logic for App.xaml.
///
/// Dual mode:
///   - Normal:    opens the GUI window.
///   - Headless:  "--screenshot &lt;path&gt; [--width N] [--height M]" renders one frame
///                offscreen, writes a PNG and exits. This is the loop a coding agent uses
///                to inspect the rendered result without any GUI interaction.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (TryGetScreenshotOptions(e.Args, out string path, out int width, out int height))
        {
            RunHeadlessScreenshot(path, width, height);
            Shutdown();
            return;
        }

        new MainWindow().Show();
    }

    private static void RunHeadlessScreenshot(string path, int width, int height)
    {
        using var context = new OffscreenContext(width, height);
        using var renderer = new Renderer();
        renderer.Initialize();
        renderer.Resize(width, height);
        renderer.RenderFrame();
        ScreenshotWriter.Save(renderer.ReadPixels(), renderer.Width, renderer.Height, path);
    }

    private static bool TryGetScreenshotOptions(string[] args, out string path, out int width, out int height)
    {
        path = "screenshot.png";
        width = 1024;
        height = 768;
        bool requested = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--screenshot":
                    requested = true;
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                        path = args[++i];
                    break;
                case "--width":
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out int w))
                        width = w;
                    break;
                case "--height":
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out int h))
                        height = h;
                    break;
            }
        }

        return requested;
    }
}
