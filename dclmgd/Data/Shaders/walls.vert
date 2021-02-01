#version 460 core

uniform mat4 world;

layout(std140) uniform matrices
{
    mat4 projection, view;
};

layout(location = 0) in vec3 position;
layout(location = 1) in vec3 normal;
layout(location = 2) in vec2 uv;
layout(location = 3) in vec4 color;

out vec4 fs_color;

void main()
{
    gl_Position = projection * view * world * vec4(position, 1);
    fs_color = color;
}