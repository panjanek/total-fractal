#version 430 core

// Display pass: samples the texture produced by the compute shader and writes it
// to the (offscreen) framebuffer. Keep this dumb - all the work happens in solve.comp.

in vec2 vUv;
out vec4 fragColor;

uniform sampler2D srcTex; // bound to texture unit 0

void main()
{
    // Force opaque alpha so the saved PNG has a solid (black) background rather than a
    // transparent one - the scatter texture's background texels have alpha 0.
    fragColor = vec4(texture(srcTex, vUv).rgb, 1.0);
}
