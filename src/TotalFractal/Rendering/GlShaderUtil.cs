using OpenTK.Graphics.OpenGL4;
using TotalFractal.Infrastructure;

namespace TotalFractal.Rendering;

/// <summary>Shared compile/link plumbing for the program wrapper classes.</summary>
internal static class GlShaderUtil
{
    public static int Compile(ShaderType type, string source, string label)
    {
        int shader = GL.CreateShader(type);
        GL.ShaderSource(shader, source);
        GL.CompileShader(shader);
        GlDebug.CheckShaderCompile(shader, label);
        return shader;
    }

    public static int Link(string label, params int[] shaders)
    {
        int program = GL.CreateProgram();
        foreach (int s in shaders)
            GL.AttachShader(program, s);

        GL.LinkProgram(program);
        GlDebug.CheckProgramLink(program, label);

        // Shaders are no longer needed once linked.
        foreach (int s in shaders)
        {
            GL.DetachShader(program, s);
            GL.DeleteShader(s);
        }
        return program;
    }
}
