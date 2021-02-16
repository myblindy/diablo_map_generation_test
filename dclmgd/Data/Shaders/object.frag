#version 460 core

uniform sampler2D diffuseTexture;

in vec2 fs_uv;

out vec4 color;

void main()
{
    color = texture(diffuseTexture, fs_uv);
}