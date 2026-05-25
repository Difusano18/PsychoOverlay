sampler screenTexture : register(s0);
sampler distortionTexture : register(s1);
sampler distortionExclusionTexture : register(s2);

float globalTime;
float2 frameDrawOffset;

float4 PixelShaderFunction(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0, float4 position : SV_Position) : COLOR0
{
    float4 distortionData = tex2D(distortionTexture, coords);
    float distortionAngle = distortionData.r * 6.2831853 + globalTime * (1 - distortionData.b) * 3;
    float2 distortionOffset = float2(cos(distortionAngle), sin(distortionAngle)) * distortionData.g * 0.12;
    
    distortionOffset *= smoothstep(1, 0, tex2D(distortionExclusionTexture, coords + distortionOffset).a);
    distortionOffset *= smoothstep(1, 0, tex2D(distortionExclusionTexture, coords).a);
    
    return tex2D(screenTexture, coords + distortionOffset);
}

technique Technique1
{
    pass AutoloadPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}