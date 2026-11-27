using System.Collections.Generic;
using EgressLab.Decision;
using UnityEngine;

namespace EgressLab.Simulation
{
    /// <summary>
    /// Builds the floor, walls, and door markers from <see cref="BuildingLayout"/>. Colliders exist only
    /// on the walkable slab and the walls, which is what the runtime NavMesh bake reads.
    /// </summary>
    public static class FloorBuilder
    {
        public static Result Build(Transform parent)
        {
            var result = new Result();
            result.Root = new GameObject("Geometry");
            result.Root.transform.SetParent(parent, false);

            CreateGround(result.Root.transform, result);
            CreateFloor(result.Root.transform, result);
            CreateWalls(result.Root.transform);
            CreateDoors(result.Root.transform, result);
            return result;
        }

        static void CreateGround(Transform parent, Result result)
        {
            Mesh mesh = HorizontalMesh(78f, 56f);
            result.Owned.Add(mesh);
            var go = new GameObject("Ground");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(BuildingLayout.Width * 0.5f - 39f, -0.04f, BuildingLayout.Depth * 0.5f - 28f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Palette.GroundMaterial;
        }

        static void CreateFloor(Transform parent, Result result)
        {
            Mesh mesh = HorizontalMesh(BuildingLayout.Width, BuildingLayout.Depth);
            result.Owned.Add(mesh);
            Texture2D grid = BuildGridTexture();
            var visual = new GameObject("Floor");
            visual.transform.SetParent(parent, false);
            visual.transform.position = new Vector3(0f, 0.02f, 0f);
            visual.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = visual.AddComponent<MeshRenderer>();
            Material material = new Material(Palette.FloorMaterial);
            material.color = Color.white;
            material.mainTexture = grid;
            renderer.sharedMaterial = material;
            result.Owned.Add(material);
            result.Owned.Add(grid);

            var solid = new GameObject("FloorCollider");
            solid.transform.SetParent(parent, false);
            solid.transform.position = new Vector3(BuildingLayout.Width * 0.5f, -0.2f, BuildingLayout.Depth * 0.5f);
            var box = solid.AddComponent<BoxCollider>();
            box.size = new Vector3(BuildingLayout.Width, 0.4f, BuildingLayout.Depth);
            result.ColliderRoot = parent.gameObject;
        }

        static Texture2D BuildGridTexture()
        {
            const int pixelsPerMetre = 4;
            int w = Mathf.RoundToInt(BuildingLayout.Width * pixelsPerMetre);
            int d = Mathf.RoundToInt(BuildingLayout.Depth * pixelsPerMetre);
            var tex = new Texture2D(w, d, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.name = "FloorGrid";
            var pixels = new Color[w * d];
            for (int y = 0; y < d; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool line = x % pixelsPerMetre == 0 || y % pixelsPerMetre == 0;
                    pixels[y * w + x] = line ? Palette.FloorLine : Palette.Floor;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false);
            return tex;
        }

        static void CreateWalls(Transform parent)
        {
            IReadOnlyList<WallBox> walls = BuildingLayout.Walls;
            for (int i = 0; i < walls.Count; i++)
            {
                WallBox wall = walls[i];
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "Wall " + i;
                cube.transform.SetParent(parent, false);
                cube.transform.position = new Vector3(wall.CenterX, BuildingLayout.WallHeight * 0.5f, wall.CenterZ);
                cube.transform.localScale = new Vector3(Mathf.Max(0.05f, wall.SizeX), BuildingLayout.WallHeight, Mathf.Max(0.05f, wall.SizeZ));
                cube.GetComponent<Renderer>().sharedMaterial = Palette.WallMaterial;
            }
        }

        static void CreateDoors(Transform parent, Result result)
        {
            // along: the axis the doorway spans. West/east doors span Z; north/south span X.
            AddDoor(parent, result, "west", new Vector3(0.28f, 0f, 7.5f), Vector3.forward, 3f);
            AddDoor(parent, result, "east", new Vector3(39.72f, 0f, 9.5f), Vector3.forward, 3f);
            AddDoor(parent, result, "north", new Vector3(17.5f, 0f, 23.72f), Vector3.right, 3f);
            AddDoor(parent, result, "south", new Vector3(27.5f, 0f, 0.28f), Vector3.right, 3f);
        }

        static void AddDoor(Transform parent, Result result, string id, Vector3 center, Vector3 along, float width)
        {
            var frames = new List<Renderer>();
            frames.Add(Post(parent, center + along * (width * 0.5f), along));
            frames.Add(Post(parent, center - along * (width * 0.5f), along));
            frames.Add(Lintel(parent, center, along, width));

            var labelGo = new GameObject(id + " label");
            labelGo.transform.SetParent(parent, false);
            labelGo.transform.position = center + Vector3.up * 0.08f + Inward(id) * 1.6f;
            labelGo.transform.rotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
            var text = labelGo.AddComponent<TextMesh>();
            text.text = id.ToUpperInvariant();
            text.font = Palette.Font;
            text.fontSize = 48;
            text.characterSize = 0.12f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = Palette.Good;

            result.Exits.Add(new ExitMark
            {
                Id = id,
                Frames = frames.ToArray(),
                Label = text
            });
        }

        static Vector3 Inward(string id)
        {
            if (id == "west") return Vector3.right;
            if (id == "east") return Vector3.left;
            if (id == "north") return Vector3.back;
            return Vector3.forward;
        }

        static Renderer Post(Transform parent, Vector3 position, Vector3 along)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Jamb";
            cube.transform.SetParent(parent, false);
            cube.transform.position = position + Vector3.up * 1.45f;
            cube.transform.localScale = new Vector3(
                Mathf.Abs(along.x) > 0.5f ? 0.22f : 0.28f,
                2.9f,
                Mathf.Abs(along.z) > 0.5f ? 0.22f : 0.28f);
            Object.DestroyImmediate(cube.GetComponent<Collider>());
            var renderer = cube.GetComponent<Renderer>();
            renderer.sharedMaterial = Palette.OpenExitMaterial;
            return renderer;
        }

        static Renderer Lintel(Transform parent, Vector3 center, Vector3 along, float width)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Lintel";
            cube.transform.SetParent(parent, false);
            cube.transform.position = center + Vector3.up * 2.85f;
            cube.transform.localScale = new Vector3(
                Mathf.Abs(along.x) > 0.5f ? width : 0.22f,
                0.18f,
                Mathf.Abs(along.z) > 0.5f ? width : 0.22f);
            Object.DestroyImmediate(cube.GetComponent<Collider>());
            var renderer = cube.GetComponent<Renderer>();
            renderer.sharedMaterial = Palette.OpenExitMaterial;
            return renderer;
        }

        public static Mesh HorizontalMesh(float width, float depth)
        {
            var mesh = new Mesh();
            mesh.vertices = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(width, 0f, 0f),
                new Vector3(0f, 0f, depth),
                new Vector3(width, 0f, depth)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public sealed class ExitMark
        {
            public string Id;
            public Renderer[] Frames;
            public TextMesh Label;
        }

        public sealed class Result
        {
            public GameObject Root;
            public GameObject ColliderRoot;
            public readonly List<ExitMark> Exits = new List<ExitMark>();
            public readonly List<Object> Owned = new List<Object>();

            public void DestroyOwned()
            {
                for (int i = 0; i < Owned.Count; i++)
                {
                    if (Owned[i] != null) Object.Destroy(Owned[i]);
                }

                Owned.Clear();
            }

            public void PaintExits(IReadOnlyList<ExitState> exits)
            {
                for (int i = 0; i < Exits.Count; i++)
                {
                    ExitState state = null;
                    for (int e = 0; e < exits.Count; e++)
                    {
                        if (exits[e].Id == Exits[i].Id) state = exits[e];
                    }

                    if (state == null) continue;
                    Material mat = state.Blocked ? Palette.BlockedExitMaterial : Palette.OpenExitMaterial;
                    Color ink = state.Blocked ? Palette.Danger : Palette.Good;
                    Renderer[] frames = Exits[i].Frames;
                    for (int f = 0; f < frames.Length; f++) frames[f].sharedMaterial = mat;
                    if (Exits[i].Label != null) Exits[i].Label.color = ink;
                }
            }
        }
    }
}
