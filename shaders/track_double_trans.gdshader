shader_type spatial;
render_mode blend_mix, cull_disabled;

// OpenBVE track shader - double sided with transparency color key
// Ported from gt-openbve tex_2_side_trans.shader

uniform sampler2D day_texture : hint_albedo;
uniform vec4 day_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform vec3 trans_color : source_color = vec3(0.0, 0.0, 0.0);
uniform float trans_tolerance : hint_range(0.0, 1.0) = 0.01;
uniform float roughness : hint_range(0.0, 1.0) = 0.8;
uniform float specular : hint_range(0.0, 1.0) = 0.1;

varying vec2 v_uv;
varying vec3 v_normal;
varying bool v_front_facing;

void vertex() {
    v_uv = UV;
    v_normal = NORMAL;
    v_front_facing = !FRAGCOORD_FACE;
}

void fragment() {
    vec4 tex = texture(day_texture, v_uv);
    vec3 albedo = tex.rgb * day_color.rgb;
    float alpha = tex.a * day_color.a;

    // Transparency color key
    vec3 diff = abs(tex.rgb - trans_color);
    float is_trans = step(max(diff.r, max(diff.g, diff.b)), trans_tolerance);
    alpha *= (1.0 - is_trans);

    if (alpha < 0.01) {
        discard;
    }

    ALBEDO = albedo;
    ROUGHNESS = roughness;
    SPECULAR = specular;
    ALPHA = alpha;
}

void light() {
    DIFFUSE_LIGHT = vec3(1.0);
    SPECULAR_LIGHT = vec3(0.0);
}