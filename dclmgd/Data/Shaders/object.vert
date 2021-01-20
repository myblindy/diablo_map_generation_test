#version 460 core

uniform mat4 worldTransform;

layout(std140) uniform matrices
{
    mat4 projection, world;
};

in vec3 position;
in vec3 normal;
in vec2 uv;

out vec4 fs_color;

void main()
{
    gl_Position = vec4(position, 0);
    fs_color = vec4(1, 1, 1, 1);
}