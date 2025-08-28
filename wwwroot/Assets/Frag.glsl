#version 300 es
precision highp float;

// ------------------------------------------------------------
// INPUTS / OUTPUTS
// ------------------------------------------------------------
in vec2 vUv;
out vec4 FragColor;

uniform vec2 u_resolution;
uniform int u_num_spheres;
uniform int u_frame;

uniform vec3 u_camPos;
uniform vec3 u_camDir;

// ------------------------------------------------------------
// DATA STRUCTURES
// ------------------------------------------------------------
struct Ray {
    vec3 origin;
    vec3 dir;
};

struct HitInfo {
    bool didHit;
    float dst;
    vec3 hitPoint;
    vec3 normal;
    vec4 colour;

    // Material properties
    vec4 emissionColour;
    float emissionStrength;
    float roughness;
    float metallic;
    float specularTint;

    bool isPlane;
};

struct Sphere {
    vec3 position;
    float radius;
    vec4 colour;
    vec4 emissionColour;
    vec4 extra; 
    // Sphere.extra mapping:
    // x = emissionStrength 
    // y = roughness (0 = perfect mirror, 1 = fully diffuse)
    // z = metallic (0 = dielectric, 1 = metal)
    // w = specularTint (0 = white specular, 1 = tinted by albedo)
};

layout(std140) uniform SpheresUBO {
    Sphere spheres[16];
};

// ------------------------------------------------------------
// CONSTANTS
// ------------------------------------------------------------
const float EPSILON = 0.001;
const float PI = 3.1415926;

vec3 SkyColourHorizon = vec3(0.8, 0.9, 1.0);
vec3 SkyColourZenith  = vec3(0.2, 0.4, 0.8);
vec3 GroundColour     = vec3(0.3, 0.25, 0.2);

vec3 LightDir         = normalize(vec3(0.2, 0.9, 0.3));
float SunIntensity    = 1.0;
float SunFocus        = 40.0;

// ------------------------------------------------------------
// RAY-OBJECT INTERSECTION
// ------------------------------------------------------------

// Sphere intersection (quadratic solution)
HitInfo RaySphere(Ray ray, Sphere sphere) {
    HitInfo hitInfo;
    hitInfo.didHit = false;
    hitInfo.dst = 1e9;
    hitInfo.colour = sphere.colour;

    vec3 oc = ray.origin - sphere.position;
    float a = dot(ray.dir, ray.dir);
    float b = 2.0 * dot(oc, ray.dir);
    float c = dot(oc, oc) - sphere.radius * sphere.radius;
    float disc = b*b - 4.0*a*c;

    if (disc >= 0.0) {
        float t = (-b - sqrt(disc)) / (2.0 * a);
        if (t > EPSILON) {
            hitInfo.didHit = true;
            hitInfo.dst = t;
            hitInfo.hitPoint = ray.origin + ray.dir * t;
            hitInfo.normal = normalize(hitInfo.hitPoint - sphere.position);
            hitInfo.emissionColour = sphere.emissionColour;
            hitInfo.emissionStrength = sphere.extra.x;
            hitInfo.roughness = sphere.extra.y;
            hitInfo.metallic = sphere.extra.z;
            hitInfo.specularTint = sphere.extra.w;
        }
    }
    return hitInfo;
}

// Infinite ground plane (y = -planeHeight, facing up)
HitInfo RayPlane(Ray ray, vec3 normal, float planeHeight) {
    HitInfo hitInfo;
    hitInfo.didHit = false;
    hitInfo.dst = 1e9;
    hitInfo.isPlane = false;

    float denom = dot(ray.dir, normal);
    if (abs(denom) > 1e-6) {
        float t = -(dot(ray.origin, normal) + planeHeight) / denom;
        if (t > EPSILON) {
            hitInfo.didHit = true;
            hitInfo.dst = t;
            hitInfo.hitPoint = ray.origin + ray.dir * t;
            hitInfo.normal = normal;
            hitInfo.isPlane = true;

            // Checkerboard pattern
            float scale = 0.5;
            vec2 coords = hitInfo.hitPoint.xz * scale;
            float checker = mod(floor(coords.x) + floor(coords.y), 2.0);
            hitInfo.colour = vec4(mix(vec3(1.0), vec3(0.1), checker), 1.0);

            // Polished material look
            hitInfo.emissionColour = vec4(0.0);
            hitInfo.emissionStrength = 0.0;
            hitInfo.roughness = 0.2;
            hitInfo.metallic = 0.6;
            hitInfo.specularTint = 0.0;
        }
    }
    return hitInfo;
}

// Find closest hit among all objects
HitInfo CalculateRayCollision(Ray ray) {
    HitInfo closest;
    closest.didHit = false;
    closest.dst = 1e9;

    // Spheres
    for (int i = 0; i < u_num_spheres; i++) {
        HitInfo h = RaySphere(ray, spheres[i]);
        if (h.didHit && h.dst < closest.dst) closest = h;
    }

    // Ground plane (y = -0.5)
    HitInfo plane = RayPlane(ray, vec3(0,1,0), 0.5);
    if (plane.didHit && plane.dst < closest.dst) closest = plane;

    return closest;
}

// ------------------------------------------------------------
// RANDOM GENERATION (PCG HASH)
// ------------------------------------------------------------
uint NextRandom(inout uint state) {
    state = state * 747796405u + 2891336453u;
    uint res = ((state >> ((state >> 28) + 4u)) ^ state) * 277803737u;
    return (res >> 22) ^ res;
}
float RandomValue(inout uint state) { return float(NextRandom(state)) / 4294967295.0; }

vec2 RandomPointInCircle(inout uint state) {
    float r = sqrt(RandomValue(state));
    float theta = 2.0 * PI * RandomValue(state);
    return r * vec2(cos(theta), sin(theta));
}

vec3 CosineWeightedHemisphere(vec3 normal, inout uint state) {
    float u1 = RandomValue(state);
    float u2 = RandomValue(state);

    float r = sqrt(u1);
    float theta = 2.0 * PI * u2;

    vec3 tangent = normalize(cross(normal, abs(normal.y) < 0.999 ? vec3(0,1,0) : vec3(1,0,0)));
    vec3 bitangent = cross(normal, tangent);

    return normalize(r * cos(theta) * tangent + r * sin(theta) * bitangent + sqrt(1.0 - u1) * normal);
}

// ------------------------------------------------------------
// LIGHTING
// ------------------------------------------------------------
float ShadowRay(vec3 point, vec3 lightDir) {
    Ray shadow;
    shadow.origin = point + lightDir * EPSILON;
    shadow.dir = lightDir;
    HitInfo hit = CalculateRayCollision(shadow);
    return hit.didHit ? 0.0 : 1.0;
}

vec3 GetEnvironmentLight(Ray ray) {
    float tSky = pow(smoothstep(0.0, 0.4, ray.dir.y), 0.35);
    float tGround = smoothstep(-0.01, 0.0, ray.dir.y);

    vec3 sky = mix(SkyColourHorizon, SkyColourZenith, tSky);
    vec3 env = mix(GroundColour, sky, tGround);

    if (tGround >= 1.0) {
        float sun = pow(max(dot(ray.dir, LightDir), 0.0), SunFocus) * SunIntensity;
        sun = min(sun, 2.0);
        env += sun * ShadowRay(ray.origin, LightDir);
    }
    return env;
}

vec3 SampleSun(vec3 hitPoint, vec3 normal, inout uint state) {
    float NdotL = max(dot(normal, LightDir), 0.0);
    float shadow = ShadowRay(hitPoint, LightDir);
    return NdotL * shadow * SunIntensity * 0.5 * vec3(1.0);
}

// Fresnel-Schlick
vec3 FresnelSchlick(float cosTheta, vec3 F0) {
    float x = 1.0 - cosTheta;
    float x5 = x*x*x*x*x; // cheaper than pow(x,5)
    return F0 + (1.0 - F0) * x5;
}

// ------------------------------------------------------------
// PATH TRACING CORE
// ------------------------------------------------------------
vec4 Trace(Ray ray, inout uint rngState) {
    vec4 incoming = vec4(0.0);
    vec4 throughput = vec4(1.0);

    bool prevPlane = false;
    const int maxBounces = 4;

    for (int bounce = 0; bounce <= maxBounces; bounce++) {
        HitInfo hit = CalculateRayCollision(ray);

        if (!hit.didHit) {
            if (!prevPlane) incoming += vec4(GetEnvironmentLight(ray), 1.0) * throughput;
            else incoming += throughput;
            break;
        }

        prevPlane = hit.isPlane;

        // Update ray origin slightly to prevent self-intersection
        ray.origin = hit.hitPoint + hit.normal * EPSILON;

        // Material params
        vec3 albedo = hit.colour.rgb;
        float roughness = clamp(hit.roughness, 0.0, 1.0);
        float metallic = clamp(hit.metallic, 0.0, 1.0);

        // Direct light & emission
        vec3 direct = 0.1 * SampleSun(hit.hitPoint, hit.normal, rngState);
        vec3 emitted = hit.emissionColour.rgb * hit.emissionStrength;
        incoming.rgb += (emitted + direct) * throughput.rgb;

        // Reflection/refraction
        vec3 diffuseDir = CosineWeightedHemisphere(hit.normal, rngState);
        vec3 specDir = reflect(ray.dir, hit.normal);

        float cosTheta = max(dot(-ray.dir, hit.normal), 0.0);
        vec3 F0 = mix(vec3(0.04), albedo, metallic);
        vec3 fresnel = FresnelSchlick(cosTheta, F0);
        float specProb = max(fresnel.r, max(fresnel.g, fresnel.b));

        bool useSpecular = RandomValue(rngState) < specProb;
        ray.dir = normalize(useSpecular ? mix(specDir, diffuseDir, roughness * roughness) : diffuseDir);
        throughput.rgb *= useSpecular ? fresnel : albedo * (1.0 - metallic);

        // Russian roulette termination
        if (bounce > 2) {
            float p = max(throughput.r, max(throughput.g, throughput.b));
            if (RandomValue(rngState) > p || p <= 0.0) break;
            throughput.rgb /= p;
        }
    }
    return incoming;
}

// ------------------------------------------------------------
// MAIN
// ------------------------------------------------------------
void main() {
    vec2 uv = vUv * 2.0 - 1.0;
    uv.x *= u_resolution.x / u_resolution.y;

    // Camera basis
    vec3 camOrigin  = u_camPos;
    vec3 camForward = normalize(u_camDir);
    vec3 camRight   = normalize(cross(vec3(0,1,0), camForward));
    vec3 camUp      = cross(camForward, camRight);

    // Random seed per pixel
    uint pixelIndex = uint(gl_FragCoord.y) * uint(u_resolution.x) + uint(gl_FragCoord.x);
    uint seed = pixelIndex + uint(u_frame) * 719393u;

    // Camera params
    float focalLength = 2.0;
    float aperture = 0.05;
    float diverge = 0.05;

    // Accumulate samples
    vec4 accumulated = vec4(0.0);
    const int numSamples = 5;

    for (int i = 0; i < numSamples; i++) {
        // Lens jitter (depth of field)
        vec2 lensJitter = RandomPointInCircle(seed) * aperture / u_resolution.x;
        vec3 rayOrigin = camOrigin + camRight * lensJitter.x + camUp * lensJitter.y;

        // Pixel jitter (anti-aliasing)
        vec2 jitter = RandomPointInCircle(seed) * diverge;
        vec3 focusPoint = camOrigin + (camForward + (uv.x + jitter.x / u_resolution.y) * camRight + (uv.y + jitter.y / u_resolution.y) * camUp) * focalLength;

        Ray r;
        r.origin = rayOrigin;
        r.dir = normalize(focusPoint - rayOrigin);

        accumulated += Trace(r, seed);
    }

    // Average and output
    vec3 hdr = accumulated.rgb / float(numSamples);
    FragColor = vec4(hdr, 1.0);
}
