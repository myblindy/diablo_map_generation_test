#version 460 core

layout(location = 0) uniform mat4 model;

layout(std140) uniform matrices
{
    mat4 projection, view;
};

layout(location = 0) in vec3 position;
layout(location = 1) in vec3 normal;
layout(location = 2) in vec2 uv;
layout(location = 3) in vec3 T;
layout(location = 4) in vec3 B;


out vec3 fs_position;
out vec3 fs_normal;
out vec2 fs_uv;


void main()
{

    fs_position = vec3(model * vec4(position, 1.0));

    fs_normal = transpose(inverse(mat3(model))) * normal;
    fs_uv = uv;


    gl_Position = projection * view * vec4(fs_position, 1.0);
}
