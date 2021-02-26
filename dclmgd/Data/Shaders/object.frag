#version 460 core

struct Material
{
    sampler2D diffuse;
    float shininess;
};

struct Light
{
    vec3 position;

    vec3 ambient;
    vec3 diffuse;
    vec3 specular;

    float constant;
    float linear;
    float quadratic;
};

in vec3 fs_position;
in vec3 fs_normal;
in vec2 fs_uv;

uniform vec3 view_position;
uniform Material material;
uniform Light light;

out vec4 color;

void main()
{
    vec3 diffuseTexel = texture(material.diffuse, fs_uv).rgb;

    // ambient
    vec3 ambient = light.ambient * diffuseTexel;

    // diffuse
    vec3 norm = normalize(fs_normal);
    vec3 lightDir = normalize(light.position - fs_position);
    float diff = max(dot(norm, lightDir), 0.0);
    vec3 diffuse = light.diffuse * diff * diffuseTexel;

    // specular
    vec3 viewDir = normalize(view_position - fs_position);
    vec3 reflectDir = reflect(-lightDir, norm);
    float spec = pow(max(dot(viewDir, reflectDir), 0.0), material.shininess);
    vec3 specular = light.specular * spec * diffuseTexel /* spec texture here */;

    // attenuation
    float distance = length(light.position - fs_position);
    float attenuation = 1.0 / (light.constant + light.linear * distance + light.quadratic * (distance * distance));

    color = vec4((ambient + diffuse + specular) * attenuation, 1.0);
}
