#version 460 core

uniform mat4 model;

layout(location = 0) in vec3 position;

layout(location = 5) in ivec4 boneIds; 
layout(location = 6) in vec4 weights;

const int MAX_BONES = 20;
const int MAX_BONE_INFLUENCE = 4;
uniform mat4 finalBoneMatrices[MAX_BONES];

void main()
{
    vec4 totalPosition = vec4(0.0);

    for(int i = 0; i < MAX_BONE_INFLUENCE; i++)
    {
        if(boneIds[i] == -1) break;

        mat4 boneTransform = finalBoneMatrices[boneIds[i]];

        vec4 localPosition = boneTransform * vec4(position, 1.0);
        totalPosition += localPosition * weights[i];
   }

    gl_Position = model * totalPosition;

}
