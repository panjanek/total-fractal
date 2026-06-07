using OpenTK.Graphics.OpenGL4;

namespace TotalFractal.Rendering;

/// <summary>
/// A 2D texture with immutable storage. Because immutable storage cannot be resized,
/// <see cref="Allocate"/> recreates the underlying GL texture object when the size changes.
/// </summary>
public sealed class Texture : IDisposable
{
    public int Handle { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }
    public SizedInternalFormat Format { get; }

    public Texture(SizedInternalFormat format = SizedInternalFormat.Rgba32f)
    {
        Format = format;
        Handle = 0;
    }

    public void Allocate(int width, int height)
    {
        if (Handle != 0)
            GL.DeleteTexture(Handle);

        Handle = GL.GenTexture();
        Width = width;
        Height = height;

        GL.BindTexture(TextureTarget.Texture2D, Handle);
        GL.TexStorage2D(TextureTarget2d.Texture2D, 1, Format, width, height);
        // Linear so a texture shown in a downscaled inset panel reads cleanly; at a 1:1
        // maximized panel Linear sampled at texel centers is equivalent to Nearest.
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        GL.BindTexture(TextureTarget.Texture2D, 0);
    }

    /// <summary>Bind for sampling on the given texture unit.</summary>
    public void Bind(int unit)
    {
        GL.ActiveTexture(TextureUnit.Texture0 + unit);
        GL.BindTexture(TextureTarget.Texture2D, Handle);
    }

    /// <summary>Bind as an image for compute shader load/store on the given image unit.</summary>
    public void BindImage(int unit, TextureAccess access)
    {
        GL.BindImageTexture(unit, Handle, 0, false, 0, access, Format);
    }

    public void Dispose()
    {
        if (Handle != 0)
        {
            GL.DeleteTexture(Handle);
            Handle = 0;
        }
    }
}
