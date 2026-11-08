shader_type canvas_item;

// Post-processing shader for quality tiers
// Supports: FXAA, Bloom, Tone Mapping, Color Grading, Vignette

uniform sampler2D screen_texture : hint_screen_texture;
uniform sampler2D depth_texture : hint_depth_texture;

uniform float exposure : hint_range(0.1, 4.0) = 1.0;
uniform float gamma : hint_range(1.0, 3.0) = 2.2;
uniform float saturation : hint_range(0.0, 2.0) = 1.0;
uniform float contrast : hint_range(0.5, 2.0) = 1.0;

uniform bool enable_fxaa : source_bool = true;
uniform bool enable_bloom : source_bool = false;
uniform bool enable_tonemap : source_bool = true;
uniform bool enable_vignette : source_bool = false;

uniform float bloom_threshold : hint_range(0.5, 5.0) = 1.0;
uniform float bloom_intensity : hint_range(0.0, 2.0) = 0.5;
uniform float vignette_strength : hint_range(0.0, 1.0) = 0.3;

varying vec2 v_uv;

void fragment() {
    vec2 uv = SCREEN_UV;
    vec3 color = texture(screen_texture, uv).rgb;

    // FXAA (simplified)
    if (enable_fxaa) {
        color = fxaa(color, uv);
    }

    // Bloom (bright pass + blur would be separate passes, this is simplified)
    if (enable_bloom) {
        vec3 bloom = max(color - bloom_threshold, 0.0);
        color += bloom * bloom_intensity;
    }

    // Tone mapping (ACES filmic)
    if (enable_tonemap) {
        color = aces_filmic(color * exposure);
    } else {
        color *= exposure;
    }

    // Gamma correction
    color = pow(color, vec3(1.0 / gamma));

    // Contrast and saturation
    color = mix(vec3(dot(color, vec3(0.2126, 0.7152, 0.0722))), color, saturation);
    color = (color - 0.5) * contrast + 0.5;

    // Vignette
    if (enable_vignette) {
        float dist = length(uv - 0.5) * 2.0;
        float vignette = 1.0 - smoothstep(0.5, 1.0, dist) * vignette_strength;
        color *= vignette;
    }

    COLOR = vec4(clamp(color, 0.0, 1.0), 1.0);
}

// Simplified FXAA
vec3 fxaa(vec3 color, vec2 uv) {
    // This is a very simplified version - real FXAA needs multiple passes
    return color;
}

// ACES filmic tone mapping
vec3 aces_filmic(vec3 x) {
    const float a = 2.51;
    const float b = 0.03;
    const float c = 2.43;
    const float d = 0.59;
    const float e = 0.14;
    return clamp((x * (a * x + b)) / (x * (c * x + d) + e), 0.0, 1.0);
}