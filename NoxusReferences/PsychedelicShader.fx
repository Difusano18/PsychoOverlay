sampler screenTexture : register(s0);
sampler warpNoiseTexture : register(s1);
sampler psychedelicTexture : register(s2);
sampler metaballTexture : register(s3);

float globalTime;
float intensity;
float baseWarpOffsetMax;
float colorInfluenceFactor;
float2 zoom;
float2 dreamcatcherCenter;

float4 CalculatePsychedelicColor(float2 coords)
{
    float2 polar = float2(atan2(coords.y + 0.5, coords.x - 0.5) / 6.283 + 0.5, distance(coords, 0.5));
    float4 color = 0;
    float2 warp = tex2D(warpNoiseTexture, polar * float2(1, 0.5) - globalTime * 0.04).rg * 0.1;
    
    // Standard FBM with a bit of extra spice added. Nothing special.
    for (int i = 0; i < 4; i++)
    {
        float2 scrollDirection = float2(sin(i * 2) - 0.2, 0);
        float2 localCoords = coords * pow(1.2, i) + scrollDirection * globalTime * 0.1 + warp;
        color += tex2D(psychedelicTexture, localCoords) / pow(1.6, i);
        warp = color.rb * 0.38;
    }
    
    return color;
}

float4 PixelShaderFunction(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0, float4 position : SV_Position) : COLOR0
{
    float distanceToDreamcatcher = distance(position.xy, dreamcatcherCenter);
    float warpOffsetMax = (baseWarpOffsetMax + clamp(0, 0.06, pow(40 / distanceToDreamcatcher, 2.5))) * intensity;
    
    float2 warpOffset = 0;
    float2 worldCoords = coords;
    for (int i = 0; i < 4; i++)
    {
        float2 scroll = float2(globalTime * i * 0.05, 0);
        float x = tex2D(warpNoiseTexture, worldCoords * (0.9 + i * 0.5) + warpOffset.x + scroll) - 0.5;
        float y = tex2D(warpNoiseTexture, worldCoords * (1.1 + i * 0.4) + warpOffset.y - scroll) - 0.5;
        warpOffset = float2(x, y) * warpOffsetMax;
    }
    
    float2 zoomedCoords = (coords - 0.5) / zoom + 0.5;
    bool coveredByMetaball = any(tex2D(metaballTexture, zoomedCoords)) || any(tex2D(metaballTexture, zoomedCoords + warpOffset));
    
    float4 psychedelicColor = CalculatePsychedelicColor(coords) * (1 - coveredByMetaball);
    float4 baseColor = tex2D(screenTexture, coords + warpOffset) + psychedelicColor * length(warpOffset) * colorInfluenceFactor;
    return baseColor;
}

technique Technique1
{
    pass AutoloadPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
