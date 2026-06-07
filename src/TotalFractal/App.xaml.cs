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
    private static readonly Vector2 SeedMin = new(-1f, -1f);
    private static readonly Vector2 SeedMax = new(1f, 1f);
    private static readonly Vector2 DefaultViewCenter = new(0f, 0f);
    private const float DefaultViewHalfHeight = 2.5f;

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
            int maxIter = TryGetIntFlag(e.Args, "--maxiter", out int mi) ? mi : 200;
            int display = TryGetIntFlag(e.Args, "--display", out int d) ? d : 0;
            Vector2 viewCenter = DefaultViewCenter;
            float viewHalfHeight = DefaultViewHalfHeight;
            if (TryGetView(e.Args, out Vector2 vc, out float vh))
            {
                viewCenter = vc;
                viewHalfHeight = vh;
            }
            // --coeffpair p,q uses 1-based coefficient numbers (e.g. 1,7 = a1,a7); -1,-1 = none.
            int coeffI = -1, coeffJ = -1;
            if (TryGetCoeffPair(e.Args, out int ci, out int cj))
            {
                coeffI = ci;
                coeffJ = cj;
            }
            RunHeadlessScreenshot(path, width, height, map, axis, points, splatSize, iterations, maxIter, display,
                viewCenter, viewHalfHeight, coeffI, coeffJ);
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
        int axisCount, int pointsPerAxis, int splatSize, int iterations, int maxIterations, int displayMode,
        Vector2 viewCenter, float viewHalfHeight, int coeffI, int coeffJ)
    {
        using var context = new OffscreenContext(width, height);
        using var renderer = new Renderer();
        renderer.Initialize();
        renderer.Resize(width, height);
        renderer.SetSeeds(axisCount, pointsPerAxis, SeedMin, SeedMax);
        renderer.SetMap(map, splatSize - 1, iterations, maxIterations); // splat size 1 = single pixel (radius 0)
        renderer.SetCoeffPair(coeffI, coeffJ);
        renderer.SetView(viewCenter, viewHalfHeight);
        renderer.SetDisplayMode(displayMode); // clamped against the active panel count
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

    /// <summary>Parse "--view cx,cy,h" (three invariant-culture floats: center + half-height).</summary>
    private static bool TryGetView(string[] args, out Vector2 center, out float halfHeight)
    {
        center = default;
        halfHeight = 0f;
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] != "--view")
                continue;

            string[] parts = args[i + 1].Split(',');
            if (parts.Length != 3)
                return false;

            if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float cx) &&
                float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float cy) &&
                float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float h))
            {
                center = new Vector2(cx, cy);
                halfHeight = h;
                return true;
            }
            return false;
        }
        return false;
    }

    /// <summary>Parse "--coeffpair p,q" (1-based coefficient numbers 1..12) into 0-based i&lt;j.</summary>
    private static bool TryGetCoeffPair(string[] args, out int i, out int j)
    {
        i = -1;
        j = -1;
        for (int k = 0; k < args.Length - 1; k++)
        {
            if (args[k] != "--coeffpair")
                continue;

            string[] parts = args[k + 1].Split(',');
            if (parts.Length != 2 ||
                !int.TryParse(parts[0], out int p) || !int.TryParse(parts[1], out int q) ||
                p < 1 || p > 12 || q < 1 || q > 12 || p == q)
                return false;

            i = Math.Min(p, q) - 1;
            j = Math.Max(p, q) - 1;
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
