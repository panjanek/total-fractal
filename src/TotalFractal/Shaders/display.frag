#version 430 core

// Display pass: samples the texture produced by the compute shader and writes it
// to the (offscreen) framebuffer. Keep this dumb - all the work happens in solve.comp.

in vec2 vUv;
out vec4 fragColor;

uniform sampler2D srcTex; // bound to texture unit 0

// Crosshair marker overlay, drawn only on the coefficients-fractal panel. markerUv is the selected
// (a[CoeffI], a[CoeffJ]) point in [0,1] panel UV (y up); panelPx is the panel size in pixels so the
// arms keep a constant thickness whether the panel is the inset or the full window.
uniform int  showMarker;   // 1 = draw the crosshair, 0 = plain texture
uniform vec2 markerUv;
uniform vec2 panelPx;

void main()
{
    // Force opaque alpha so the saved PNG has a solid (black) background rather than a
    // transparent one - the scatter texture's background texels have alpha 0.
    vec3 col = texture(srcTex, vUv).rgb;

    if (showMarker == 1)
    {
        vec2 d = abs(vUv - markerUv) * panelPx;   // pixel distance from the marker centre
        const float ARM  = 9.0;   // arm half-length (px)
        const float GAP  = 3.0;   // empty centre half-size (px) - leaves the picked texel readable
        const float CORE = 1.0;   // white arm half-thickness (px)
        const float EDGE = 2.0;   // dark-outline half-thickness (px)
        bool hCore = d.y <= CORE && d.x <= ARM && d.x >= GAP;
        bool vCore = d.x <= CORE && d.y <= ARM && d.y >= GAP;
        bool hEdge = d.y <= EDGE && d.x <= ARM + (EDGE - CORE) && d.x >= GAP;
        bool vEdge = d.x <= EDGE && d.y <= ARM + (EDGE - CORE) && d.y >= GAP;
        if (hCore || vCore)      col = vec3(1.0);   // white arm (visible on the black interior)
        else if (hEdge || vEdge) col = vec3(0.0);   // dark outline (visible on the bright palette)
    }

    fragColor = vec4(col, 1.0);
}
