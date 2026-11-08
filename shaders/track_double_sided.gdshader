shader_type spatial;
render_mode unshaded, cull_disabled;

// OpenBVE track shader - double sided textured
// Ported from gt-openbve tex_2_side.shader

uniform sampler2D day_texture : hint_albedo;
uniform vec4 day_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform float roughness : hint_range(0.0, 1.0) = 0.8;
uniform float specular : hint_range(0.0, 1.0) = 0.1;

varying vec2 v_uv;
varying vec3 v_normal;
varying bool v_front_facing;

void vertex() {
    v_uv = UV;
    v_normal = NORMAL;
    v_front_facing = !FRAGCOORD_FACE; // true for front face
}

void fragment() {
    vec4 tex = texture(day_texture, v_uv);
    vec3 albedo = tex.rgb * day_color.rgb;
    float alpha = tex.a * day_color.a;

    // For back face, we might want to flip normal or UV
    // In OpenBVE, double-sided just means both sides visible with same texture
    ALBEDO = albedo;
    ROUGHNESS = roughness;
    SPECULAR = specular;
    ALPHA = alpha;
}

void light() {
    DIFFUSE_LIGHT = vec3(1.0);
    SPECULAR_LIGHT = vec3(0.0);
}