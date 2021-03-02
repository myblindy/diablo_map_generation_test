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

layout(location = 5) in ivec4 boneIds; 
layout(location = 6) in vec4 weights;

const int MAX_BONES = 100;
const int MAX_BONE_INFLUENCE = 4;
uniform mat4 finalBonesMatrices[MAX_BONES];

out vec3 fs_position;
out vec3 fs_normal;
out vec2 fs_uv;

out mat3 TBN;

void main()
{
    vec4 totalPosition = vec4(0.0f);
    for(int i = 0; i < MAX_BONE_INFLUENCE; i++)
    {
        if(boneIds[i] == -1) 
            continue;
        if(boneIds[i] >= MAX_BONES) 
        {
            totalPosition = vec4(position, 1.0f);
            break;
        }
        vec4 localPosition = finalBoneMatrices[boneIds[i]] * vec4(position, 1.0f);
        totalPosition += localPosition * weights[i];
        //vec3 localNormal = mat3(finalBoneMatrices[boneIds[i]]) * norm;
   }

    fs_position = vec3(model * vec4(totalPosition, 1.0));
    fs_normal = transpose(inverse(mat3(model))) * normal;
    fs_uv = uv;

    TBN = mat3(T, B, normal);

    gl_Position = projection * view * vec4(fs_position, 1.0);
}
