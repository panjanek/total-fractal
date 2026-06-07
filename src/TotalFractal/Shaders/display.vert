#version 430 core

// Fullscreen pass: emits a single oversized triangle covering the whole viewport.
// No vertex buffer is needed - positions and UVs are derived from gl_VertexID.
// Draw with: glDrawArrays(GL_TRIANGLES, 0, 3) and an (empty) bound VAO.

out vec2 vUv;

void main()
{
    // gl_VertexID -> 0,1,2
    //  uv: (0,0) (2,0) (0,2)  => covers [0,1]x[0,1] after clipping
    //  pos: (-1,-1) (3,-1) (-1,3)
    vec2 uv = vec2((gl_VertexID << 1) & 2, gl_VertexID & 2);
    vUv = uv;
    gl_Position = vec4(uv * 2.0 - 1.0, 0.0, 1.0);
}
