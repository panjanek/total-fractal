using System.IO;
using System.Reflection;

namespace TotalFractal.Infrastructure;

/// <summary>
/// Loads shader text. Shaders live as files under <c>Shaders/</c> and are embedded into
/// the assembly (see the EmbeddedResource glob in the .csproj). At runtime they are read
/// back via <see cref="Assembly.GetManifestResourceStream(string)"/>.
///
/// In DEBUG builds we first try to read the file straight from the source tree so shader
/// edits take effect on the next run without rebuilding; we fall back to the embedded copy.
/// </summary>
public static class EmbeddedShaderSource
{
    private static readonly Assembly Asm = typeof(EmbeddedShaderSource).Assembly;

    /// <summary>Load a shader by file name, e.g. <c>Load("display.vert")</c>.</summary>
    public static string Load(string fileName)
    {
#if DEBUG
        string? diskPath = TryFindOnDisk(fileName);
        if (diskPath != null)
            return File.ReadAllText(diskPath);
#endif
        string resourceName = ResolveResourceName(fileName);
        using Stream stream = Asm.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException(
                $"Embedded shader resource '{resourceName}' could not be opened.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>The logical names of all embedded resources - handy for debugging mismatches.</summary>
    public static string[] ResourceNames() => Asm.GetManifestResourceNames();

    private static string ResolveResourceName(string fileName)
    {
        // Embedded names look like "TotalFractal.Shaders.display.vert". Match by suffix so
        // the root namespace / folder layout doesn't need to be hardcoded here.
        string suffix = "." + fileName;
        foreach (string name in Asm.GetManifestResourceNames())
        {
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ||
                name.Equals(fileName, StringComparison.OrdinalIgnoreCase))
                return name;
        }

        throw new FileNotFoundException(
            $"No embedded resource matching '{fileName}'. Available: " +
            string.Join(", ", Asm.GetManifestResourceNames()));
    }

#if DEBUG
    private static string? TryFindOnDisk(string fileName)
    {
        // Walk up from the output directory (bin/Debug/net8.0-windows) to the project
        // root, looking for the Shaders folder in the source tree.
        string? dir = AppContext.BaseDirectory;
        for (int i = 0; i < 6 && dir != null; i++)
        {
            string candidate = Path.Combine(dir, "Shaders", fileName);
            if (File.Exists(candidate))
                return candidate;
            dir = Directory.GetParent(dir)?.FullName;
        }
        return null;
    }
#endif
}
