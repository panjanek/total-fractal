using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace TotalFractal.Rendering;

/// <summary>A linked compute shader program. Requires an OpenGL 4.3+ context.</summary>
public sealed class ComputeProgram : IDisposable
{
    public int Handle { get; }
    private readonly Dictionary<string, int> _uniforms = new();

    public ComputeProgram(string computeSource, string label = "compute")
    {
        int cs = GlShaderUtil.Compile(ShaderType.ComputeShader, computeSource, label + ".comp");
        Handle = GlShaderUtil.Link(label, cs);
    }

    public void Use() => GL.UseProgram(Handle);

    /// <summary>Dispatch work groups. Caller is responsible for the appropriate memory barrier.</summary>
    public void Dispatch(int groupsX, int groupsY, int groupsZ = 1)
        => GL.DispatchCompute(groupsX, groupsY, groupsZ);

    public int GetUniformLocation(string name)
    {
        if (!_uniforms.TryGetValue(name, out int loc))
        {
            loc = GL.GetUniformLocation(Handle, name);
            _uniforms[name] = loc;
        }
        return loc;
    }

    public void SetInt(string name, int value) => GL.Uniform1(GetUniformLocation(name), value);
    public void SetFloat(string name, float value) => GL.Uniform1(GetUniformLocation(name), value);
    public void SetVec2(string name, Vector2 value) => GL.Uniform2(GetUniformLocation(name), value);

    public void Dispose() => GL.DeleteProgram(Handle);
}
