//-----------------------------------------------------------------------------
// Simple.fx - for SpriteRenderer
// Takes [Position, Color, TexCoords] and draws a textured sprite
//-----------------------------------------------------------------------------
#include "Simple.fxh"

technique Simple
{
    pass P0
    {
#if VULKAN
        VertexShader = compile vs_6_0 SimpleVertexShader();
        PixelShader = compile ps_6_0 SimplePixelShader();
#else
        VertexShader = compile vs_4_0_level_9_1 SimpleVertexShader();
        PixelShader = compile ps_4_0_level_9_1 SimplePixelShader();
#endif
    }
}
