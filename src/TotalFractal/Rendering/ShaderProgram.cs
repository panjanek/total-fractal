using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace TotalFractal.Rendering;

/// <summary>A linked vertex + fragment shader program, with cached uniform locations.</summary>
public sealed class ShaderProgram : IDisposable
{
    public int Handle { get; }
    private readonly Dictionary<string, int> _uniforms = new();

    public ShaderProgram(string vertexSource, string fragmentSource, string label = "program")
    {
        int vs = GlShaderUtil.Compile(ShaderType.VertexShader, vertexSource, label + ".vert");
        int fs = GlShaderUtil.Compile(ShaderType.FragmentShader, fragmentSource, label + ".frag");
        Handle = GlShaderUtil.Link(label, vs, fs);
    }

    public void Use() => GL.UseProgram(Handle);

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
    public void SetVec3(string name, Vector3 value) => GL.Uniform3(GetUniformLocation(name), value);

    public void Dispose() => GL.DeleteProgram(Handle);
}
