#version 430 core
out vec4 color;

in vec2 tex;

layout(rgba32f, binding = 0) uniform sampler2D image;

uniform float iterations;

void main() {
	color = vec4(texture2D(image, tex).xyz / iterations, 1.0);
}
