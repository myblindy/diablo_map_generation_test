#version 460 core

layout(location = 0) uniform mat4 model;

layout(std140) uniform matrices
{
    mat4 projection, view;
    float timeSec;
};

layout(location = 0) in vec3 position;
layout(location = 1) in vec3 normal;
layout(location = 2) in vec2 uv;
layout(location = 3) in vec3 T;
layout(location = 4) in vec3 B;

layout(location = 5) in ivec4 boneIds; 
layout(location = 6) in vec4 weights;

const int MAX_BONES = 20;
const int MAX_BONE_INFLUENCE = 4;
uniform mat4 finalBoneMatrices[MAX_BONES];

out vec3 fs_position;
out vec3 fs_normal;
out vec2 fs_uv;

out mat3 TBN;

void main()
{
    fs_uv = uv;

    vec4 totalPosition = vec4(0.0);
    vec4 totalNormal = vec4(0.0);

    for(int i = 0; i < MAX_BONE_INFLUENCE; i++)
    {
        if(boneIds[i] == -1) break;

        mat4 boneTransform = finalBoneMatrices[boneIds[i]];

        vec4 localPosition = boneTransform * vec4(position, 1.0);
        totalPosition += localPosition * weights[i];

        vec4 localNormal = boneTransform * vec4(normal, 0.0);
        totalNormal += localNormal * weights[i];
   }

    fs_position = vec3(model * totalPosition);
    fs_normal = transpose(inverse(mat3(model))) * totalNormal.xyz;


    TBN = mat3(T, B, fs_normal);

    gl_Position = projection * view * vec4(fs_position, 1.0);
}
