using System;

namespace EgressLab.Decision
{
    /// <summary>
    /// Smoke and fire on a 1 m grid. Cells only ever get hotter, which keeps the spread easy to test.
    /// A burning cell (intensity at or above <see cref="BurningThreshold"/>) pushes heat into its
    /// four neighbours; warm cells ramp toward 1. One seeded ember each step nudges the front so
    /// two seeds are not identical, without starting new fires across the floor.
    /// </summary>
    public sealed class HazardField
    {
        public const float SpreadPush = 0.085f;
        public const float Intensify = 0.11f;
        public const float Ember = 0.04f;
        public const float BurningThreshold = 0.75f;

        readonly float[] _cells;
        int _revision;

        public HazardField(int width, int depth)
        {
            if (width < 1 || depth < 1) throw new ArgumentOutOfRangeException("width", "Hazard grid must be at least 1x1.");
            Width = width;
            Depth = depth;
            _cells = new float[width * depth];
        }

        public int Width { get; }
        public int Depth { get; }

        /// <summary>Bumps whenever the grid changes, so a view can skip redundant texture uploads.</summary>
        public int Revision { get { return _revision; } }

        public float this[int x, int z]
        {
            get { return _cells[Index(x, z)]; }
        }

        public void Set(int x, int z, float value)
        {
            _cells[Index(x, z)] = Clamp01(value);
            _revision++;
        }

        public float[] CopyCells()
        {
            var copy = new float[_cells.Length];
            Array.Copy(_cells, copy, _cells.Length);
            return copy;
        }

        /// <summary>
        /// Elliptical ignition against the west door. The west approach starts above the block
        /// threshold; the other three doors start cold. This is the scenario the demo compares.
        /// </summary>
        public static HazardField IgniteDefault()
        {
            var field = new HazardField(BuildingLayout.CellsX, BuildingLayout.CellsZ);
            const float cx = 2.1f;
            const float cz = 7.5f;
            const float rx = 3.2f;
            const float rz = 2.5f;
            for (int z = 0; z < field.Depth; z++)
            {
                for (int x = 0; x < field.Width; x++)
                {
                    float dx = (x + 0.5f - cx) / rx;
                    float dz = (z + 0.5f - cz) / rz;
                    float radius = (float)Math.Sqrt(dx * dx + dz * dz);
                    if (radius < 1f)
                    {
                        float intensity = 1f - 0.45f * radius;
                        if (intensity < 0.55f) intensity = 0.55f;
                        field._cells[field.Index(x, z)] = Clamp01(intensity);
                    }
                }
            }

            field._revision++;
            return field;
        }

        public float Sample(WorldPos position)
        {
            if (position.X < 0f || position.Z < 0f || position.X >= Width || position.Z >= Depth) return 0f;
            int x = (int)position.X;
            int z = (int)position.Z;
            if (x >= Width) x = Width - 1;
            if (z >= Depth) z = Depth - 1;
            return _cells[Index(x, z)];
        }

        public void Step(float spreadRate, Random random)
        {
            if (spreadRate <= 0f) return;
            float rate = spreadRate > 3f ? 3f : spreadRate;
            var next = (float[])_cells.Clone();
            int hotIndex = 0;
            float hotValue = -1f;

            for (int z = 0; z < Depth; z++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int i = Index(x, z);
                    float value = _cells[i];
                    if (value > hotValue)
                    {
                        hotValue = value;
                        hotIndex = i;
                    }

                    if (value > 0.02f && value < 1f)
                    {
                        float grown = value + Intensify * rate * (1f - value);
                        if (grown > 1f) grown = 1f;
                        if (grown > next[i]) next[i] = grown;
                    }

                    if (value >= BurningThreshold)
                    {
                        float push = SpreadPush * rate;
                        Add(next, x + 1, z, push);
                        Add(next, x - 1, z, push);
                        Add(next, x, z + 1, push);
                        Add(next, x, z - 1, push);
                    }
                }
            }

            if (hotValue > 0.3f && random != null)
            {
                int direction = random.Next(4);
                float extra = Ember * rate * (0.5f + (float)random.NextDouble());
                int hx = hotIndex % Width;
                int hz = hotIndex / Width;
                int nx = hx;
                int nz = hz;
                if (direction == 0) nx++;
                else if (direction == 1) nx--;
                else if (direction == 2) nz++;
                else nz--;
                if (!Add(next, nx, nz, extra))
                {
                    float stamped = next[hotIndex] + extra;
                    if (stamped > 1f) stamped = 1f;
                    next[hotIndex] = stamped;
                }
            }

            Array.Copy(next, _cells, _cells.Length);
            _revision++;
        }

        bool Add(float[] next, int x, int z, float amount)
        {
            if (amount <= 0f) return true;
            if (x < 0 || z < 0 || x >= Width || z >= Depth) return false;
            int i = Index(x, z);
            float value = next[i] + amount;
            if (value > 1f) value = 1f;
            if (value > next[i]) next[i] = value;
            return true;
        }

        int Index(int x, int z)
        {
            if (x < 0 || z < 0 || x >= Width || z >= Depth)
                throw new ArgumentOutOfRangeException("x", "Hazard cell (" + x + ", " + z + ") is outside the grid.");
            return z * Width + x;
        }

        static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }
    }
}
