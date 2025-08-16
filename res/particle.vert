#version 330 core

layout (location = 0) in vec2 aPos;
layout (location = 1) in float aPressure;

out vec3 VertColor;

uniform mat4 projection;
uniform float minPressure;
uniform float maxPressure;

void main()
{
    gl_Position = projection * vec4(aPos, 0.0, 1.0);

    float clamped_pressure = clamp((aPressure - minPressure) / (maxPressure - minPressure), 0.0, 1.0);
    VertColor = mix(vec3(0.0, 0.4, 1), vec3(1, 1, 1), clamped_pressure);
}