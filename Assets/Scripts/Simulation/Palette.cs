using UnityEngine;

namespace EgressLab.Simulation
{
    /// <summary>
    /// Shared colours and the one white sprite the overlay uses. Built-in render pipeline, no asset files.
    /// </summary>
    public static class Palette
    {
        public static readonly Color Background = new Color(0.07f, 0.08f, 0.10f, 1f);
        public static readonly Color Floor = new Color(0.22f, 0.25f, 0.30f, 1f);
        public static readonly Color FloorLine = new Color(0.16f, 0.18f, 0.22f, 1f);
        public static readonly Color Ground = new Color(0.05f, 0.055f, 0.065f, 1f);
        public static readonly Color Wall = new Color(0.78f, 0.74f, 0.66f, 1f);
        public static readonly Color Ink = new Color(0.93f, 0.94f, 0.95f, 1f);
        public static readonly Color Muted = new Color(0.68f, 0.72f, 0.76f, 1f);
        public static readonly Color Panel = new Color(0.07f, 0.08f, 0.10f, 0.88f);
        public static readonly Color PanelEdge = new Color(1f, 1f, 1f, 0.08f);
        public static readonly Color Amber = new Color(0.95f, 0.66f, 0.16f, 1f);
        public static readonly Color Cyan = new Color(0.29f, 0.78f, 0.86f, 1f);
        public static readonly Color Good = new Color(0.36f, 0.80f, 0.58f, 1f);
        public static readonly Color Danger = new Color(0.90f, 0.32f, 0.28f, 1f);
        public static readonly Color Caught = new Color(0.42f, 0.10f, 0.14f, 1f);
        public static readonly Color Button = new Color(0.16f, 0.18f, 0.22f, 1f);

        static Sprite _white;
        static Material _floor;
        static Material _wall;
        static Material _ground;
        static Material _amber;
        static Material _cyan;
        static Material _caught;
        static Material _open;
        static Material _blocked;
        static Material _hazard;

        public static Sprite White
        {
            get
            {
                if (_white != null) return _white;
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
                tex.Apply();
                _white = Sprite.Create(tex, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 2f);
                _white.name = "EgressWhite";
                return _white;
            }
        }

        public static Font Font
        {
            get
            {
                Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                return font;
            }
        }

        public static Material FloorMaterial { get { return _floor ?? (_floor = Lit(Floor, 0.18f)); } }
        public static Material WallMaterial { get { return _wall ?? (_wall = Lit(Wall, 0.12f)); } }
        public static Material GroundMaterial { get { return _ground ?? (_ground = Lit(Ground, 0.05f)); } }
        public static Material AmberMaterial { get { return _amber ?? (_amber = Lit(Amber, 0.35f)); } }
        public static Material CyanMaterial { get { return _cyan ?? (_cyan = Lit(Cyan, 0.35f)); } }
        public static Material CaughtMaterial { get { return _caught ?? (_caught = Lit(Caught, 0.1f)); } }
        public static Material OpenExitMaterial { get { return _open ?? (_open = Emissive(Good)); } }
        public static Material BlockedExitMaterial { get { return _blocked ?? (_blocked = Emissive(Danger)); } }

        public static Material HazardMaterial(Texture texture)
        {
            if (_hazard == null)
            {
                Shader shader = Shader.Find("Unlit/Transparent");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Standard");
                _hazard = new Material(shader);
                _hazard.color = Color.white;
                if (_hazard.HasProperty("_Cull")) _hazard.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            }

            _hazard.mainTexture = texture;
            return _hazard;
        }

        public static void DestroyCreated()
        {
            Destroy(ref _floor);
            Destroy(ref _wall);
            Destroy(ref _ground);
            Destroy(ref _amber);
            Destroy(ref _cyan);
            Destroy(ref _caught);
            Destroy(ref _open);
            Destroy(ref _blocked);
            Destroy(ref _hazard);
            if (_white != null)
            {
                if (_white.texture != null) Object.Destroy(_white.texture);
                Object.Destroy(_white);
                _white = null;
            }
        }

        static void Destroy(ref Material material)
        {
            if (material == null) return;
            Object.Destroy(material);
            material = null;
        }

        static Material Lit(Color color, float smoothness)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Diffuse");
            var mat = new Material(shader);
            mat.color = color;
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            return mat;
        }

        static Material Emissive(Color color)
        {
            Material mat = Lit(color, 0.4f);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * 1.3f);
            }

            return mat;
        }
    }
}
