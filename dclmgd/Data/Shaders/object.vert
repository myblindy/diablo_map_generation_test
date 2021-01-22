#version 460 core

uniform mat4 world;

layout(std140) uniform matrices
{
    mat4 projection, view;
};

in vec3 position;
in vec3 normal;
in vec2 uv;

out vec4 fs_color;

void main()
{
    gl_Position = projection * view * world * vec4(position, 1);
    fs_color = vec4(1, 1, 1, 1);
}