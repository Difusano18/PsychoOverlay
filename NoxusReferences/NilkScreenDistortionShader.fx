sampler screenTexture : register(s0);
sampler overlayTexture : register(s2);
sampler previousScreenTexture : register(s3);
sampler noiseTexture : register(s4);

float globalTime;
float intensity;
float opacity;
float datamoshIntensity;
float4 palette[8];

float4 PaletteLerp(float interpolant)
{
    int startIndex = clamp(interpolant * 6, 0, 6);
    int endIndex = startIndex + 1;
    return lerp(palette[startIndex], palette[endIndex], frac(interpolant * 6));
}

float4 PixelShaderFunction(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    float smoothOpacity = opacity * opacity * (3 - opacity * 2);
    float effectiveIntensity = intensity * smoothOpacity;
    float offsetTime = globalTime * 0.7;
    float2 unmodifiedCoords = coords;
    coords.x += cos(offsetTime + coords.y * 6.283) * effectiveIntensity * 0.05;
    coords.y += cos(offsetTime + coords.x * 6.283) * effectiveIntensity * 0.05;
    
    // Store the original color, before any distortions are applied.
    float4 baseColor = tex2D(screenTexture, coords);
    
    // Apply wavy distortions to the distorted color.
    float2 originalCoords = coords;
    coords.y += (sin(coords.x * 300 - coords.y * 32 + globalTime * 20) * 0.004 + sin(coords.x * 20 + coords.y * 105 + globalTime * 10) * 0.003) * effectiveIntensity;
    
    float4 screenColor = tex2D(screenTexture, coords);
    float4 previousScreenColor = tex2D(previousScreenTexture, coords);
    float blendNoise = tex2D(noiseTexture, unmodifiedCoords * 1.4 + previousScreenColor.r) +
                       tex2D(noiseTexture, unmodifiedCoords * 0.9 + previousScreenColor.b);
    float blendInterpolant = smoothstep(1 - datamoshIntensity, 1, blendNoise * 0.5);
    
    // Calculate colors.
    float4 color = lerp(screenColor, previousScreenColor, blendInterpolant * pow(effectiveIntensity, 2.5));
    float blurInterpolant = smoothstep(0.2, 0.05, distance(coords, 0.5));
    float4 blurredColor = 0;
    for (int i = -6; i < 6; i++)
        blurredColor += tex2D(screenTexture, coords + float2(i, 0) * effectiveIntensity * 0.001) / 13;
    color = lerp(color, blurredColor, blurInterpolant);
    
    // Interpolate between the palette based on the luminosity of the color, along with time.
    float luminosity = dot(color.rgb, float3(0.3, 0.6, 0.1));
    float4 evilColor = PaletteLerp(sin(luminosity * 6.283 - globalTime * 1.5) * 0.5 + 0.5);
    
    // Apply a vignette.
    evilColor -= distance(coords, 0.5) * 0.6;
    
    // Apply overlays.
    float4 overlayColor = tex2D(overlayTexture, unmodifiedCoords);
    evilColor = lerp(evilColor, overlayColor, overlayColor.a);
    
    return lerp(baseColor, evilColor, effectiveIntensity);
}

technique Technique1
{
    pass AutoloadPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
