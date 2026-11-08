shader_type spatial;
render_mode cull_back;

// Train shader with support for multiple textures and lighting
// Supports: day/night textures, emission (lights), normal maps

uniform sampler2D day_texture : hint_albedo;
uniform sampler2D night_texture : hint_albedo;
uniform sampler2D normal_map : hint_normal;
uniform sampler2D emission_map : hint_albedo;

uniform vec4 day_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform vec4 night_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform float roughness : hint_range(0.0, 1.0) = 0.3;
uniform float specular : hint_range(0.0, 1.0) = 0.5;
uniform float metallic : hint_range(0.0, 1.0) = 0.1;
uniform float emission_strength : hint_range(0.0, 10.0) = 1.0;

uniform bool use_night_texture : source_bool = false;
uniform bool use_normal_map : source_bool = false;
uniform bool use_emission : source_bool = false;

varying vec2 v_uv;
varying vec3 v_normal;
varying vec3 v_tangent;
varying vec3 v_bitangent;
varying vec3 v_world_pos;

void vertex() {
    v_uv = UV;
    v_normal = NORMAL;
    v_tangent = TANGENT;
    v_bitangent = BINORMAL;
    v_world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
    vec4 day_tex = texture(day_texture, v_uv);
    vec3 albedo = day_tex.rgb * day_color.rgb;
    float alpha = day_tex.a * day_color.a;

    // Night texture blending (for lit windows, signals at night)
    if (use_night_texture) {
        vec4 night_tex = texture(night_texture, v_uv);
        // Simple blend based on time of day (would come from uniform)
        float night_factor = 0.0; // TODO: pass time of day
        albedo = mix(albedo, night_tex.rgb * night_color.rgb, night_factor);
    }

    // Normal mapping
    if (use_normal_map) {
        vec3 normal_map_sample = texture(normal_map, v_uv).xyz * 2.0 - 1.0;
        mat3 tbn = mat3(v_tangent, v_bitangent, v_normal);
        NORMAL = normalize(tbn * normal_map_sample);
    } else {
        NORMAL = v_normal;
    }

    // Emission (headlights, taillights, cabin lights)
    if (use_emission) {
        vec4 emission_tex = texture(emission_map, v_uv);
        EMISSION = emission_tex.rgb * emission_strength;
    }

    ALBEDO = albedo;
    ROUGHNESS = roughness;
    SPECULAR = specular;
    METALLIC = metallic;
    ALPHA = alpha;
}

void light() {
    // Standard PBR lighting
    DIFFUSE_LIGHT = vec3(1.0);
    SPECULAR_LIGHT = vec3(1.0);
}