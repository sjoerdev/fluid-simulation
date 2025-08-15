#version 330 core

in vec3 VertColor;

out vec4 FragColor;

void main()
{
    // discard if outside radius
    vec2 coord = gl_PointCoord - vec2(0.5);
    float dist = length(coord);
    if (dist > 0.5) discard;

    FragColor = vec4(VertColor, 1);
}