using System.Collections.Generic;
using Usage = UnityEditor.ShaderGraph.GraphDelta.GraphType.Usage;

namespace UnityEditor.ShaderGraph.Defs
{

    internal class GatherTexture2DNode : IStandardNode
    {
        public static string Name = "GatherTexture2DNode";
        public static int Version = 1;

        public static FunctionDescriptor FunctionDescriptor => new(
            Version,
            Name,
@"
{
#if (SHADER_TARGET >= 41)
    RGBA = float4(1,1,1,1);
    //RGBA = Texture.tex.Gather(Sampler.samplerstate, UV, Offset);
    RGB = RGBA.rgb;
    R = RGBA.r;
    G = RGBA.g;
    B = RGBA.b;
    A = RGBA.a;
#else
    RGBA = float4(1,1,1,1);
    //R = SAMPLE_TEXTURE2D_LOD(Texture.tex, Sampler.samplerstate, (floor(UV * Texture.texelSize.zw + temp1) + trunc(Offset) + temp2) * Texture.texelSize.xy, 0).r;
    //G = SAMPLE_TEXTURE2D_LOD(Texture.tex, Sampler.samplerstate, (floor(UV * Texture.texelSize.zw + temp2) + trunc(Offset) + temp2) * Texture.texelSize.xy, 0).r;
    //B = SAMPLE_TEXTURE2D_LOD(Texture.tex, Sampler.samplerstate, (floor(UV * Texture.texelSize.zw + temp3) + trunc(Offset) + temp2) * Texture.texelSize.xy, 0).r;
    //A = SAMPLE_TEXTURE2D_LOD(Texture.tex, Sampler.samplerstate, (floor(UV * Texture.texelSize.zw + temp4) + trunc(Offset) + temp2) * Texture.texelSize.xy, 0).r;
    //RGBA.r = R;
    //RGBA.g = G;
    //RGBA.b = B;
    //RGBA.a = A;
    RGB = RGBA.rgb;
#endif
}",
            new ParameterDescriptor("Texture", TYPE.Vec4, Usage.In),//fix type
            new ParameterDescriptor("UV", TYPE.Vec2, Usage.In),//add default UVs
            new ParameterDescriptor("Sampler", TYPE.Vec2, Usage.In),//fix type
            new ParameterDescriptor("Offset", TYPE.Vec2, Usage.In),
            new ParameterDescriptor("RGBA", TYPE.Vec4, Usage.Out),
            new ParameterDescriptor("RGB", TYPE.Vec3, Usage.Out),//this is new.  Should we keep it?
            new ParameterDescriptor("R", TYPE.Float, Usage.Out),
            new ParameterDescriptor("G", TYPE.Float, Usage.Out),
            new ParameterDescriptor("B", TYPE.Float, Usage.Out),
            new ParameterDescriptor("A", TYPE.Float, Usage.Out),
            new ParameterDescriptor("temp1", TYPE.Vec2, Usage.Local, new float[] { -0.5f, 0.5f}),
            new ParameterDescriptor("temp2", TYPE.Vec2, Usage.Local, new float[] { 0.5f, 0.5f }),
            new ParameterDescriptor("temp3", TYPE.Vec2, Usage.Local, new float[] { 0.5f, -0.5f }),
            new ParameterDescriptor("temp4", TYPE.Vec2, Usage.Local, new float[] { -0.5f, -0.5f })
        );

        public static NodeUIDescriptor NodeUIDescriptor => new(
            Version,
            Name,
            tooltip: "takes 4 samples (x component only) to use for bilinear interp during sampling",
            categories: new string[2] { "Input", "Texture" },
            synonyms: new string[0],
            parameters: new ParameterUIDescriptor[10] {
                new ParameterUIDescriptor(
                    name: "Texture",
                    tooltip: "the texture asset to sample"
                ),
                new ParameterUIDescriptor(
                    name: "UV",
                    tooltip: "the texture coordinates to use for sampling the texture"
                ),
                new ParameterUIDescriptor(
                    name: "Sampler",
                    tooltip: "the texture sampler to use for sampling the texture"
                ),
                new ParameterUIDescriptor(
                    name: "RGBA",
                    tooltip: "A vector4 from the sampled texture"
                ),
                new ParameterUIDescriptor(
                    name: "RGB",
                    tooltip: "A vector3 from the sampled texture"
                ),
                new ParameterUIDescriptor(
                    name: "R",
                    tooltip: "the red channel of the sampled texture"
                ),
                new ParameterUIDescriptor(
                    name: "G",
                    tooltip: "the green channel of the sampled texture"
                ),
                new ParameterUIDescriptor(
                    name: "B",
                    tooltip: "the blue channel of the sampled texture"
                ),
                new ParameterUIDescriptor(
                    name: "A",
                    tooltip: "the alpha channel of the sampled texture"
                ),
                new ParameterUIDescriptor(
                    name: "Offset",
                    tooltip: "texture coordinate offset"
                )
            }
        );
    }
}
