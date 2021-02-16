#version 460 core

uniform mat4 world;

layout(std140) uniform matrices
{
    mat4 projection, view;
};

layout(location = 0) in vec3 position;
layout(location = 1) in vec3 normal;
layout(location = 2) in vec2 uv;

out vec2 fs_uv;

void main()
{
    gl_Position = projection * view * world * vec4(position, 1);
    fs_uv = uv;
}