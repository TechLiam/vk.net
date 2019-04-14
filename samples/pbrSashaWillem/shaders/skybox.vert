#version 450

#extension GL_ARB_separate_shader_objects : enable
#extension GL_ARB_shading_language_420pack : enable

layout (location = 0) in vec3 inPos;
layout (location = 1) in vec2 inUV;

layout (location = 0) out vec3 outUVW;

layout (binding = 0) uniform UBO 
{
    mat4 projection;
    mat4 view;
    mat4 model;
    vec4 lightPos;
    float gamma;
    float exposure;    
} ubo;

out gl_PerVertex 
{
    vec4 gl_Position;   
};

void main()
{
    outUVW = inPos;
    gl_Position = ubo.projection * ubo.view * vec4(inPos, 1.0);
}
