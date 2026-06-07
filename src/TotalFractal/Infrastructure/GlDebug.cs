using OpenTK.Graphics.OpenGL4;

namespace TotalFractal.Infrastructure;

/// <summary>
/// Small helpers that turn silent OpenGL failures into exceptions with readable messages.
/// </summary>
public static class GlDebug
{
    public static void CheckShaderCompile(int shader, string label)
    {
        GL.GetShader(shader, ShaderParameter.CompileStatus, out int status);
        if (status == 0)
        {
            string log = GL.GetShaderInfoLog(shader);
            throw new InvalidOperationException($"Shader compile failed ({label}):\n{log}");
        }
    }

    public static void CheckProgramLink(int program, string label)
    {
        GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int status);
        if (status == 0)
        {
            string log = GL.GetProgramInfoLog(program);
            throw new InvalidOperationException($"Program link failed ({label}):\n{log}");
        }
    }

    public static void CheckFramebuffer(string label)
    {
        FramebufferErrorCode status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != FramebufferErrorCode.FramebufferComplete)
            throw new InvalidOperationException($"Framebuffer incomplete ({label}): {status}");
    }

    /// <summary>Throws if any GL error is pending. Call after a logical block of GL work.</summary>
    public static void Check(string where)
    {
        ErrorCode err = GL.GetError();
        if (err != ErrorCode.NoError)
            throw new InvalidOperationException($"OpenGL error {err} at {where}");
    }
}
