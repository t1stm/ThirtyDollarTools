#version 330 core

in vec2 fragmentCoords; // expected 0..1
in vec4 RGBA;

out vec4 color;

void main()
{
    vec2 radius = vec2(0.5);
    
    // move to center space (-0.5 .. 0.5)
    vec2 p = fragmentCoords - vec2(0.5);

    // ellipse equation
    vec2 n = p / radius;
    float d = dot(n, n);

    // anti-aliasing
    float aa = fwidth(d);
    float alpha = 1.0 - smoothstep(1.0 - aa, 1.0 + aa, d);

    if (alpha < 0.01)
    {
        discard;
    }

    color = vec4(1.0) * RGBA;
    color.a *= alpha;
}
