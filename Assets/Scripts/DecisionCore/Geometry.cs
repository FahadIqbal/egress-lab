using System;

namespace EgressLab.Decision
{
    /// <summary>
    /// A point on the floor, in metres. X is east, Z is north. Y is up and is not part of the plan.
    /// </summary>
    public readonly struct WorldPos
    {
        public readonly float X;
        public readonly float Z;

        public WorldPos(float x, float z)
        {
            X = x;
            Z = z;
        }

        public float Length
        {
            get { return (float)Math.Sqrt(X * X + Z * Z); }
        }

        public float DistanceTo(WorldPos other)
        {
            float dx = X - other.X;
            float dz = Z - other.Z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        public static WorldPos operator +(WorldPos a, WorldPos b)
        {
            return new WorldPos(a.X + b.X, a.Z + b.Z);
        }

        public static WorldPos operator -(WorldPos a, WorldPos b)
        {
            return new WorldPos(a.X - b.X, a.Z - b.Z);
        }

        public static WorldPos operator *(WorldPos a, float scale)
        {
            return new WorldPos(a.X * scale, a.Z * scale);
        }

        public override string ToString()
        {
            return "(" + X.ToString("0.00") + ", " + Z.ToString("0.00") + ")";
        }
    }

    /// <summary>
    /// Axis-aligned wall footprint. The matching mesh is extruded upward by the Unity builder.
    /// </summary>
    public readonly struct WallBox
    {
        public readonly float CenterX;
        public readonly float CenterZ;
        public readonly float SizeX;
        public readonly float SizeZ;

        public WallBox(float centerX, float centerZ, float sizeX, float sizeZ)
        {
            CenterX = centerX;
            CenterZ = centerZ;
            SizeX = sizeX;
            SizeZ = sizeZ;
        }

        public bool OverlapsCell(float minX, float minZ, float size)
        {
            float maxX = minX + size;
            float maxZ = minZ + size;
            float wMinX = CenterX - SizeX * 0.5f;
            float wMaxX = CenterX + SizeX * 0.5f;
            float wMinZ = CenterZ - SizeZ * 0.5f;
            float wMaxZ = CenterZ + SizeZ * 0.5f;
            return wMinX < maxX && wMaxX > minX && wMinZ < maxZ && wMaxZ > minZ;
        }
    }
}
