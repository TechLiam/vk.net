#version 450
#extension GL_ARB_separate_shader_objects : enable
#extension GL_ARB_shading_language_420pack : enable

layout (input_attachment_index = 0, set = 2, binding = 0) uniform subpassInputMS samplerColorRough;
layout (input_attachment_index = 1, set = 2, binding = 1) uniform subpassInputMS samplerEmitMetal;
layout (input_attachment_index = 2, set = 2, binding = 2) uniform subpassInputMS samplerN_AO;
layout (input_attachment_index = 3, set = 2, binding = 3) uniform subpassInputMS samplerPos;

layout (set = 0, binding = 1) uniform samplerCube samplerIrradiance;
layout (set = 0, binding = 2) uniform samplerCube prefilteredMap;
layout (set = 0, binding = 3) uniform sampler2D samplerBRDFLUT;

layout (push_constant) uniform PushCsts {
    layout(offset = 64)
    int imgIdx;
};

layout (location = 0) in vec2 inUV;
layout (location = 0) out vec4 outColor;

const uint color        = 0;
const uint normal       = 1;
const uint pos          = 2;
const uint occlusion    = 3;
const uint emissive     = 4;
const uint metallic     = 5;
const uint roughness    = 6;
const uint depth        = 7;
            
void main() 
{
    switch (imgIdx) {
        case color:
            outColor = vec4(subpassLoad(samplerColorRough, gl_SampleID).rgb, 1);
            break;
        case normal:
            outColor = vec4(subpassLoad(samplerN_AO, gl_SampleID).rgb, 1);
            break;
        case pos:
            outColor = vec4(subpassLoad(samplerPos, gl_SampleID).rgb, 1);
            break;
        case occlusion:
            outColor = vec4(subpassLoad(samplerN_AO, gl_SampleID).aaa, 1);
            break;
        case emissive:
            outColor = vec4(subpassLoad(samplerEmitMetal, gl_SampleID).rgb, 1);
            break;
        case metallic:
            outColor = vec4(subpassLoad(samplerEmitMetal, gl_SampleID).aaa, 1);
            break;
        case roughness:
            outColor = vec4(subpassLoad(samplerColorRough, gl_SampleID).aaa, 1);
            break;
        case depth:
            outColor = vec4(subpassLoad(samplerPos, gl_SampleID).aaa, 1);
            break;
    }
}