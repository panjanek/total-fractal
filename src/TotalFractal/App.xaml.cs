using System.Globalization;
using System.Windows;
using OpenTK.Mathematics;
using TotalFractal.Infrastructure;
using TotalFractal.Model;
using TotalFractal.Rendering;
using TotalFractal.ViewModels;
using Application = System.Windows.Application;

namespace TotalFractal;

/// <summary>
/// Interaction logic for App.xaml.
///
/// Dual mode:
///   - Normal:    opens the main GL window plus the config window (shared view model).
///   - Headless:  "--screenshot &lt;path&gt; [--width N] [--height M] [--coeffs a1,..,a12]"
///                renders one frame offscreen, writes a PNG and exits. This is the loop a
///                coding agent uses to inspect a coefficient set without GUI interaction.
/// </summary>
public partial class App : Application
{
    private static readonly Vector4 DefaultView = new(-2.5f, -2.5f, 2.5f, 2.5f);
    private static readonly Vector2 SeedMin = new(-1f, -1f);
    private static readonly Vector2 SeedMax = new(1f, 1f);

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (TryGetScreenshotOptions(e.Args, out string path, out int width, out int height))
        {
            // Resolve overrides against the GUI defaults so headless renders are deterministic.
            QuadraticMap map = TryGetCoeffs(e.Args, out QuadraticMap m) ? m : QuadraticMap.Example;
            int axis = TryGetIntFlag(e.Args, "--axis", out int a) ? a : 20;
            int points = TryGetIntFlag(e.Args, "--points", out int p) ? p : 1000;
            int splatSize = TryGetIntFlag(e.Args, "--splat", out int s) ? s : 2;
            int iterations = TryGetIntFlag(e.Args, "--iterations", out int it) ? it : 1;
            RunHeadlessScreenshot(path, width, height, map, axis, points, splatSize, iterations);
            Shutdown();
            return;
        }

        // GUI: main window + config window, both bound to one view model.
        var vm = new ParametersViewModel();
        var main = new MainWindow(vm);
        Current.MainWindow = main;
        ShutdownMode = ShutdownMode.OnMainWindowClose; // closing the main window exits the app
        main.Show();

        // Owner must be an already-shown window; place the config window beside the main one.
        var config = new ConfigWindow { DataContext = vm, Owner = main };
        config.Left = main.Left + main.ActualWidth;
        config.Top = main.Top;
        config.Show();
    }

    private static void RunHeadlessScreenshot(
        string path, int width, int height, QuadraticMap map,
        int axisCount, int pointsPerAxis, int splatSize, int iterations)
    {
        using var context = new OffscreenContext(width, height);
        using var renderer = new Renderer();
        renderer.Initialize();
        renderer.Resize(width, height);
        renderer.SetSeeds(axisCount, pointsPerAxis, SeedMin, SeedMax);
        renderer.SetMap(map, DefaultView, splatSize - 1, iterations); // splat size 1 = single pixel (radius 0)
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

    /// <summary>Parse "--coeffs a1,a2,...,a12" (12 comma-separated, invariant-culture floats).</summary>
    private static bool TryGetCoeffs(string[] args, out QuadraticMap map)
    {
        map = default;
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] != "--coeffs")
                continue;

            string[] parts = args[i + 1].Split(',');
            if (parts.Length != 12)
                return false;

            var v = new float[12];
            for (int k = 0; k < 12; k++)
                if (!float.TryParse(parts[k], NumberStyles.Float, CultureInfo.InvariantCulture, out v[k]))
                    return false;

            map = new QuadraticMap
            {
                A1 = v[0], A2 = v[1], A3 = v[2], A4 = v[3], A5 = v[4], A6 = v[5],
                A7 = v[6], A8 = v[7], A9 = v[8], A10 = v[9], A11 = v[10], A12 = v[11],
            };
            return true;
        }

        return false;
    }

    /// <summary>Parse an "--name &lt;int&gt;" flag (e.g. --axis 50).</summary>
    private static bool TryGetIntFlag(string[] args, string name, out int value)
    {
        value = 0;
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name && int.TryParse(args[i + 1], out value))
                return true;
        return false;
    }
}
