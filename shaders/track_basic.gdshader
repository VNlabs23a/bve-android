shader_type spatial;
render_mode unshaded, cull_back;

// OpenBVE track shader - single sided textured
// Ported from gt-openbve tex_1_side.shader

uniform sampler2D day_texture : hint_albedo;
uniform vec4 day_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform float roughness : hint_range(0.0, 1.0) = 0.8;
uniform float specular : hint_range(0.0, 1.0) = 0.1;

varying vec2 v_uv;
varying vec3 v_normal;
varying vec3 v_world_pos;

void vertex() {
    v_uv = UV;
    v_normal = NORMAL;
    v_world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
    vec4 tex = texture(day_texture, v_uv);
    vec3 albedo = (tex.rgb * day_color.rgb);
    float alpha = tex.a * day_color.a;

    ALBEDO = albedo;
    ROUGHNESS = roughness;
    SPECULAR = specular;
    ALPHA = alpha;
}

void light() {
    // Simple unshaded - lighting handled by vertex colors or environment
    DIFFUSE_LIGHT = vec3(1.0);
    SPECULAR_LIGHT = vec3(0.0);
}