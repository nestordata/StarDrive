//-----------------------------------------------------------------------------
// ParticleEffect.fx
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------


// Camera parameters.
float4x4 View;
float4x4 Projection;
float2 ViewportScale;

// The current time, in seconds.
float CurrentTime;

// Parameters describing how the particles animate.
float Duration;
float DurationRandomness;
float AlignRotationToVelocity;
float EndVelocity;

// [min; max] color range at start of particle life
float4 StartMinColor;
float4 StartMaxColor;
// [min; max] color range reached according to EndColorTimeMul
float4 EndMinColor;
float4 EndMaxColor;

// multiplier for controlling relativeAge when particle reaches EndColor
// could be 1.33 so it reaches EndColor faster
// or it could be 1.0 so it reaches EndColor at total end of particle life
float EndColorTimeMul;

// These float2 parameters describe the min and max of a range.
// The actual value is chosen differently for each particle,
// interpolating between x and y by some random amount.
float2 RotateSpeed;
float2 StartSize;
float2 EndSize;


// Particle texture and sampler.
#if VULKAN
// Explicit t0/s0 required: mgfxc 3.8.5 Vulkan without registers writes
// garbage textureSlot/samplerSlot (e.g. 225/226) and EffectPass.Apply OOBs.
Texture2D Texture : register(t0);
SamplerState ParticleSampler : register(s0)
{
    Filter = Linear;
    AddressU = Clamp;
    AddressV = Clamp;
};
#else
texture Texture;

sampler ParticleSampler = sampler_state
{
    Texture = (Texture);
    
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Point;
    
    AddressU = Clamp;
    AddressV = Clamp;
};
#endif


// Vertex shader input structure describes the start position and
// velocity of the particle, and the time at which it was created,
// along with some random values that affect its size and rotation.
struct VertexShaderInput
{
    // Corner on TEXCOORD2 — Vulkan/SPIR-V is unreliable with two POSITION semantics
    // (POSITION0 corner + POSITION1 world pos). Match MonoGame vertex usage index 2.
    float2 Corner : TEXCOORD2;
    float3 Position : POSITION0;
    float3 Velocity : NORMAL0;
    float4 Color : COLOR0;
    float4 Random : COLOR1;
    float Scale : TEXCOORD0;
    float Time : TEXCOORD1;
};

// Static techniques never read Velocity. Declaring NORMAL0 and then letting the
// compiler strip it leaves a SPIR-V Location hole (TEXCOORD1 stays at Loc 6).
// MonoGame DesktopVK binds Vk locations by Attributes[] index (location=i), so
// holes make TEXCOORD1 miss the descriptor → MoltenVK pipeline compile failure.
// Omit NORMAL0 here so locations stay contiguous 0..5.
struct VertexShaderInputStatic
{
    float2 Corner : TEXCOORD2;
    float3 Position : POSITION0;
    float4 Color : COLOR0;
    float4 Random : COLOR1;
    float Scale : TEXCOORD0;
    float Time : TEXCOORD1;
};


// Vertex shader output structure specifies the position and color of the particle.
struct VertexShaderOutput
{
#if VULKAN
    float4 Position : SV_POSITION;
#else
    float4 Position : POSITION0;
#endif
    float4 Color : COLOR0;
    // TEXCOORD0 — not COLOR1. COLOR interpolators for UVs break MonoGame Vulkan/
    // SPIR-V pass Apply (index OOB). DX tolerated the XNA-era COLOR1 abuse.
    float2 TextureCoordinate : TEXCOORD0;
};

// Apply the camera view and projection transforms.
float4 ToScreenCoords(float3 pos)
{
    return mul(mul(float4(pos, 1), View), Projection);
}

float4 GetPosition(float3 position, float3 velocity, float time)
{
    float V_start = length(velocity);

    // Work out how fast the particle should be moving at the end of its life,
    // by applying a constant scaling factor to its starting velocity.
    float V_end = V_start * EndVelocity;

    // Constant acceleration formula for calculating distance.
    // We are using normalized time [0.0;1.0], so Duration = 1.0
    // a = (V_end - V_start)/Duration
    // S = V_start*t + (at^2)/2
    float distance = V_start*time + (V_end-V_start)*time*time*0.5;

    // Safe-normalize: zero-velocity particles (e.g. Flash, called as
    // AddParticle(pos) which passes Vector3.Zero) hit normalize(0) here.
    // Under mgfxc / vs_4_0_level_9_1 that compiles to 0/0 = NaN, and NaN*0 = NaN,
    // so the entire quad ends up at NaN clip-space and the GPU drops it. The
    // XNA D3DX compiler must have folded this differently. Compute the unit
    // direction inline with a max-against-epsilon divisor instead — when
    // V_start==0 the velocity vector is also 0 so the result is 0/eps = 0,
    // and when V_start>0 the divisor equals V_start so it matches normalize().
    float3 dir = velocity / max(V_start, 0.0001);
    position += dir * distance * Duration;

    return ToScreenCoords(position);
}

float GetParticleSize(float randomValue, float normalizedAge)
{
    // Apply a random factor to make each particle a slightly different size.
    float startSize = lerp(StartSize.x, StartSize.y, randomValue);
    float endSize = lerp(EndSize.x, EndSize.y, randomValue);
    
    // Compute the actual size based on the age of the particle.
    float size = lerp(startSize, endSize, normalizedAge);
    
    // Project the size into screen coordinates.
    return size * Projection._m11;
}

float4 GetStaticParticleColor(float randomValue)
{
    return lerp(StartMinColor, StartMaxColor, randomValue);
}

float4 GetParticleColor(float randomValue, float normalizedAge)
{
    // get this particle's Start and End color based on `randomValue`
    // the color is interpolated linearly, so it will stay consistent
    float4 startColor = lerp(StartMinColor, StartMaxColor, randomValue);
    float4 endColor = lerp(EndMinColor, EndMaxColor, randomValue);
    
    // Alpha fades based on the age of the particle. This curve is hard coded
    // to make the particle fade in fairly quickly, then fade out more slowly.
    // The 6.75 constant scales the curve so the alpha will reach 1.0 at relativeAge=0.33
    // enter x*(1-x)*(1-x)*6.75 for x=0:1 into a plotting program to see the curve
    // https://www.desmos.com/calculator/bhcidfwd0e
    // https://www.wolframalpha.com/input/?i=x*%281-x%29*%281-x%29*6.75+for+x%3D0%3A1
    float alpha = normalizedAge * (1-normalizedAge) * (1-normalizedAge) * 6.75;

    // linearly interpolate towards end color, using EndColorTimeMul multiplier
    float colorAge = saturate(normalizedAge * EndColorTimeMul);
    float4 color = lerp(startColor, endColor, colorAge);

    color.a *= alpha;
    return color;
}

float2x2 RotationToMatrix(float rotation) // Get a 2x2 rotation matrix from rotation radians
{
    float c = cos(rotation);
    float s = sin(rotation);
    return float2x2(c, -s, s, c);
}

float2x2 GetVelocityRotation(float3 velocity)
{
    const float PI = 3.14159265358979;

    float2 dir = normalize(float2(velocity.x, velocity.y));
    
    // by default AlignRotationToVelocity = 1 means that TOP of the particle image
    // is traveling towards velocity direction
    float rot = atan2(dir.x, dir.y) - PI;
    
    if (AlignRotationToVelocity > 0)
    {
        // if AlignRotationToVelocity = +1.0, rot = 20 + (180 - 180*+1.0) = 20 + 0 = 0
        // if AlignRotationToVelocity = +0.5, rot = 20 + (180 - 180*+0.5) = 20 + 90 = 110 (+90 degrees)
        rot = rot + (PI - PI*AlignRotationToVelocity);
    }
    else
    {
        // if AlignRotationToVelocity = -1.0, rot = 20 + 180*-1.0) = 20 - 180 = -160 [rotation - PI]
        // if AlignRotationToVelocity = -0.5, rot = 20 + 180*-0.5) = 20 - 90  = -70  (-90 degrees)
        rot = rot + (PI*AlignRotationToVelocity);
    }
    return RotationToMatrix(rot);
}

float2x2 GetRandomizedRotation(float randomValue, float age)
{
    // Apply a random factor to make each particle rotate at a different speed.
    float rotateSpeed = lerp(RotateSpeed.x, RotateSpeed.y, randomValue);
    float rotation = rotateSpeed * age;
    return RotationToMatrix(rotation);
}

float GetParticleAge(float time, float random)
{
    float age = CurrentTime - time;
    // Apply a random factor to make different particles age at different rates.
    // ActualDuration = Duration + Duration*DurationRandomness*Random(0.0, 1.0)
    return age + age*DurationRandomness*random;
}

// Normalize the age into the range [0.0; 1.0]
float GetNormalizedAge(float age)
{
    return saturate(age / Duration); // clamp(x, 0, 1)
}

VertexShaderOutput DynamicParticleVS(VertexShaderInput input)
{
    VertexShaderOutput output;
    
    float age = GetParticleAge(input.Time, input.Random.x);
    float normalizedAge = GetNormalizedAge(age);
    float size = GetParticleSize(input.Random.y, normalizedAge) * input.Scale;
    float2x2 rot = GetRandomizedRotation(input.Random.w, age);

    output.Position = GetPosition(input.Position, input.Velocity, normalizedAge);
    // this cleverly scales the Quad corners
    output.Position.xy += mul(input.Corner, rot) * size * ViewportScale;
    output.Color = GetParticleColor(input.Random.z, normalizedAge) * input.Color;
    output.TextureCoordinate = (input.Corner + 1) / 2;
    return output;
}

VertexShaderOutput DynamicAlignRotationToVelocityVS(VertexShaderInput input)
{
    VertexShaderOutput output;
    
    float age = GetParticleAge(input.Time, input.Random.x);
    float normalizedAge = GetNormalizedAge(age);
    float size = GetParticleSize(input.Random.y, normalizedAge) * input.Scale;
    float2x2 rot = GetVelocityRotation(input.Velocity);

    output.Position = GetPosition(input.Position, input.Velocity, normalizedAge);
    // this cleverly scales the Quad corners
    output.Position.xy += mul(input.Corner, rot) * size * ViewportScale;
    output.Color = GetParticleColor(input.Random.z, normalizedAge) * input.Color;
    output.TextureCoordinate = (input.Corner + 1) / 2;
    return output;
}

VertexShaderOutput DynamicNonRotatingVS(VertexShaderInput input)
{
    VertexShaderOutput output;
    
    float age = GetParticleAge(input.Time, input.Random.x);
    float normalizedAge = GetNormalizedAge(age);
    float size = GetParticleSize(input.Random.y, normalizedAge) * input.Scale;

    output.Position = GetPosition(input.Position, input.Velocity, normalizedAge);
    // this cleverly scales the Quad corners
    output.Position.xy += input.Corner * size * ViewportScale;
    output.Color = GetParticleColor(input.Random.z, normalizedAge) * input.Color;
    output.TextureCoordinate = (input.Corner + 1) / 2;
    return output;
}

VertexShaderOutput StaticRotatingVS(VertexShaderInputStatic input)
{
    VertexShaderOutput output;
    
    float age = GetParticleAge(input.Time, input.Random.x);
    float normalizedAge = GetNormalizedAge(age);
    float size = GetParticleSize(input.Random.y, normalizedAge) * input.Scale;
    float2x2 rotation = GetRandomizedRotation(input.Random.w, age);

    output.Position = ToScreenCoords(input.Position);
    // this cleverly scales the Quad corners
    output.Position.xy += mul(input.Corner, rotation) * size * ViewportScale;
    output.Color = GetStaticParticleColor(input.Random.z) * input.Color;
    output.TextureCoordinate = (input.Corner + 1) / 2;
    return output;
}

VertexShaderOutput StaticNonRotatingVS(VertexShaderInputStatic input)
{
    VertexShaderOutput output;
    
    float age = GetParticleAge(input.Time, input.Random.x);
    float normalizedAge = GetNormalizedAge(age);
    float size = GetParticleSize(input.Random.y, normalizedAge) * input.Scale;

    output.Position = ToScreenCoords(input.Position);
    // this cleverly scales the Quad corners
    output.Position.xy += input.Corner * size * ViewportScale;
    output.Color = GetStaticParticleColor(input.Random.z) * input.Color;
    output.TextureCoordinate = (input.Corner + 1) / 2;
    return output;
}

// Pixel shader for drawing particles.
#if VULKAN
float4 ParticlePixelShader(VertexShaderOutput input) : SV_TARGET
{
    return Texture.Sample(ParticleSampler, input.TextureCoordinate) * input.Color;
}
#else
float4 ParticlePixelShader(VertexShaderOutput input) : COLOR0
{
    return tex2D(ParticleSampler, input.TextureCoordinate) * input.Color;
}
#endif

#if VULKAN
#define PARTICLE_VS vs_6_0
#define PARTICLE_PS ps_6_0
#else
#define PARTICLE_VS vs_4_0_level_9_1
#define PARTICLE_PS ps_4_0_level_9_1
#endif

// Dynamic particles have velocity, rotation, all features
technique FullDynamicParticles
{
    pass P0
    {
        VertexShader = compile PARTICLE_VS DynamicParticleVS();
        PixelShader = compile PARTICLE_PS ParticlePixelShader();
    }
}

technique DynamicAlignRotationToVelocityParticles
{
    pass P0
    {
        VertexShader = compile PARTICLE_VS DynamicAlignRotationToVelocityVS();
        PixelShader = compile PARTICLE_PS ParticlePixelShader();
    }
}

// Particles that move but do not rotate
technique DynamicNonRotatingParticles
{
    pass P0
    {
        VertexShader = compile PARTICLE_VS DynamicNonRotatingVS();
        PixelShader = compile PARTICLE_PS ParticlePixelShader();
    }
}

// Static particles never move, but they can rotate
technique StaticRotatingParticles
{
    pass P0
    {
        VertexShader = compile PARTICLE_VS StaticRotatingVS();
        PixelShader = compile PARTICLE_PS ParticlePixelShader();
    }
}

// Static non-rotating particles, never move, never rotate
technique StaticNonRotatingParticles
{
    pass P0
    {
        VertexShader = compile PARTICLE_VS StaticNonRotatingVS();
        PixelShader = compile PARTICLE_PS ParticlePixelShader();
    }
}
