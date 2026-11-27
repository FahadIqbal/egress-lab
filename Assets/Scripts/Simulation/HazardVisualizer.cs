using EgressLab.Decision;
using UnityEngine;

namespace EgressLab.Simulation
{
    /// <summary>
    /// One texture, one cell per metre, laid on the floor. The mesh UVs match the hazard grid:
    /// pixel (x, z) is the cell whose corner is (x, z).
    /// </summary>
    public sealed class HazardVisualizer : MonoBehaviour
    {
        Texture2D _texture;
        Mesh _mesh;
        HazardField _hazard;
        int _seenRevision = -1;
        Light _glow;

        public static HazardVisualizer Create(Transform parent, HazardField hazard)
        {
            var go = new GameObject("Hazard");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<HazardVisualizer>();
            view.Build(hazard);
            return view;
        }

        void Build(HazardField hazard)
        {
            _hazard = hazard;
            _texture = new Texture2D(hazard.Width, hazard.Depth, TextureFormat.RGBA32, false);
            _texture.wrapMode = TextureWrapMode.Clamp;
            _texture.filterMode = FilterMode.Point;
            _texture.name = "HazardGrid";

            _mesh = FloorBuilder.HorizontalMesh(BuildingLayout.Width, BuildingLayout.Depth);
            Mesh mesh = _mesh;
            var sheet = new GameObject("HazardSheet");
            sheet.transform.SetParent(transform, false);
            sheet.transform.position = new Vector3(0f, 0.05f, 0f);
            sheet.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = sheet.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Palette.HazardMaterial(_texture);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var glowGo = new GameObject("HazardLight");
            glowGo.transform.SetParent(transform, false);
            glowGo.transform.position = new Vector3(2.1f, 2.2f, 7.5f);
            _glow = glowGo.AddComponent<Light>();
            _glow.type = LightType.Point;
            _glow.color = new Color(1f, 0.45f, 0.12f);
            _glow.range = 11f;
            _glow.intensity = 2.4f;
            _glow.shadows = LightShadows.None;

            TrySmoke(glowGo.transform);
            Apply(hazard);
        }

        static void TrySmoke(Transform parent)
        {
            Shader shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) return;
            var go = new GameObject("Smoke");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, -1.4f, 0f);
            var particles = go.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.startLifetime = 3.2f;
            main.startSpeed = 0.7f;
            main.startSize = 1.4f;
            main.startColor = new Color(0.25f, 0.25f, 0.28f, 0.35f);
            main.maxParticles = 40;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = particles.emission;
            emission.rateOverTime = 6f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8f;
            shape.radius = 0.3f;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(shader);
        }

        public void Apply(HazardField hazard)
        {
            if (hazard == null || _texture == null) return;
            if (hazard.Revision == _seenRevision) return;
            _seenRevision = hazard.Revision;
            var pixels = new Color[hazard.Width * hazard.Depth];
            for (int z = 0; z < hazard.Depth; z++)
            {
                for (int x = 0; x < hazard.Width; x++)
                {
                    pixels[z * hazard.Width + x] = ColorFor(hazard[x, z]);
                }
            }

            _texture.SetPixels(pixels);
            _texture.Apply(false);
            if (_glow != null) _glow.intensity = 1.6f + hazard.Sample(new WorldPos(2.1f, 7.5f)) * 2.2f;
        }

        static Color ColorFor(float intensity)
        {
            if (intensity <= 0.015f) return new Color(0f, 0f, 0f, 0f);
            Color rgb = Color.Lerp(new Color(1f, 0.84f, 0.28f), new Color(0.86f, 0.16f, 0.08f), Mathf.Clamp01(intensity));
            rgb.a = Mathf.Lerp(0.22f, 0.78f, Mathf.Clamp01(intensity));
            return rgb;
        }

        void OnDestroy()
        {
            if (_texture != null) Destroy(_texture);
            if (_mesh != null) Destroy(_mesh);
        }
    }
}
