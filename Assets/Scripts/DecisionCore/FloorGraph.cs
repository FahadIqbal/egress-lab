using System;
using System.Collections.Generic;

namespace EgressLab.Decision
{
    /// <summary>
    /// Static walk grid for the floor. Walls are rasterised at <see cref="CellSize"/> metres and each
    /// exit gets a Dijkstra field, so "nearest" means shortest walk rather than straight-line distance.
    /// The Unity crowd still moves on a NavMesh; this graph is the decision layer (and the headless drill).
    /// </summary>
    public sealed class FloorGraph
    {
        public const float CellSize = 0.5f;

        readonly bool[] _blocked;
        readonly ExitField[] _fields;
        readonly Dictionary<string, int> _fieldIndex;

        FloorGraph(bool[] blocked, ExitField[] fields, Dictionary<string, int> fieldIndex, int cellsX, int cellsZ)
        {
            _blocked = blocked;
            _fields = fields;
            _fieldIndex = fieldIndex;
            CellsX = cellsX;
            CellsZ = cellsZ;
        }

        public int CellsX { get; }
        public int CellsZ { get; }

        public static FloorGraph Build(IReadOnlyList<WallBox> walls, IReadOnlyList<ExitState> exits)
        {
            if (exits == null) throw new ArgumentNullException("exits");
            int cellsX = (int)Math.Round(BuildingLayout.Width / CellSize);
            int cellsZ = (int)Math.Round(BuildingLayout.Depth / CellSize);
            var blocked = new bool[cellsX * cellsZ];
            for (int z = 0; z < cellsZ; z++)
            {
                for (int x = 0; x < cellsX; x++)
                {
                    float minX = x * CellSize;
                    float minZ = z * CellSize;
                    bool hit = false;
                    if (walls != null)
                    {
                        for (int w = 0; w < walls.Count; w++)
                        {
                            if (walls[w].OverlapsCell(minX, minZ, CellSize))
                            {
                                hit = true;
                                break;
                            }
                        }
                    }

                    blocked[z * cellsX + x] = hit;
                }
            }

            var fields = new ExitField[exits.Count];
            var index = new Dictionary<string, int>();
            for (int i = 0; i < exits.Count; i++)
            {
                index[exits[i].Id] = i;
                int goal = NearestWalkableIndex(blocked, cellsX, cellsZ, exits[i].Arrival);
                fields[i] = ComputeField(blocked, cellsX, cellsZ, goal);
            }

            return new FloorGraph(blocked, fields, index, cellsX, cellsZ);
        }

        public bool IsWalkable(WorldPos position)
        {
            if (position.X < 0f || position.Z < 0f || position.X >= BuildingLayout.Width || position.Z >= BuildingLayout.Depth)
                return false;
            int x = (int)(position.X / CellSize);
            int z = (int)(position.Z / CellSize);
            if (x < 0 || z < 0 || x >= CellsX || z >= CellsZ) return false;
            return !_blocked[z * CellsX + x];
        }

        public WorldPos Snap(WorldPos position)
        {
            int cell = NearestWalkable(position);
            if (cell < 0) return position;
            return Center(cell);
        }

        public float Distance(string exitId, WorldPos from)
        {
            ExitField field = Field(exitId);
            int cell = NearestWalkable(from);
            if (cell < 0) return float.PositiveInfinity;
            float distance = field.Dist[cell];
            if (float.IsInfinity(distance)) return distance;
            return distance + from.DistanceTo(Center(cell));
        }

        public WorldPos Steer(string exitId, WorldPos from, WorldPos arrival)
        {
            if (from.DistanceTo(arrival) <= 0.3f) return arrival;
            ExitField field = Field(exitId);
            int cell = NearestWalkable(from);
            if (cell < 0 || cell == field.Goal) return arrival;
            int next = field.Parent[cell];
            if (next < 0 || next == cell) return arrival;
            return Center(next);
        }

        public IReadOnlyList<WorldPos> Path(string exitId, WorldPos from, WorldPos arrival)
        {
            var path = new List<WorldPos>();
            ExitField field = Field(exitId);
            int cell = NearestWalkable(from);
            if (cell < 0) return path;
            int guard = 0;
            while (cell >= 0 && guard++ <= _blocked.Length)
            {
                path.Add(Center(cell));
                if (cell == field.Goal) break;
                int next = field.Parent[cell];
                if (next < 0 || next == cell) break;
                cell = next;
            }

            if (path.Count > 0) path[path.Count - 1] = arrival;
            return path;
        }

        public void SamplePathHazard(
            string exitId,
            WorldPos from,
            WorldPos arrival,
            HazardField hazard,
            out float mean,
            out float max)
        {
            mean = 0f;
            max = 0f;
            if (hazard == null) return;
            IReadOnlyList<WorldPos> path = Path(exitId, from, arrival);
            if (path.Count == 0)
            {
                float only = hazard.Sample(from);
                mean = only;
                max = only;
                return;
            }

            float sum = 0f;
            for (int i = 0; i < path.Count; i++)
            {
                float value = hazard.Sample(path[i]);
                sum += value;
                if (value > max) max = value;
            }

            mean = sum / path.Count;
        }

        ExitField Field(string exitId)
        {
            int index;
            if (exitId == null || !_fieldIndex.TryGetValue(exitId, out index))
                throw new ArgumentException("No distance field for exit '" + exitId + "'.", "exitId");
            return _fields[index];
        }

        int NearestWalkable(WorldPos position)
        {
            return NearestWalkableIndex(_blocked, CellsX, CellsZ, position);
        }

        WorldPos Center(int index)
        {
            int x = index % CellsX;
            int z = index / CellsX;
            return new WorldPos((x + 0.5f) * CellSize, (z + 0.5f) * CellSize);
        }

        static int NearestWalkableIndex(bool[] blocked, int cellsX, int cellsZ, WorldPos position)
        {
            int x = (int)(position.X / CellSize);
            int z = (int)(position.Z / CellSize);
            if (x < 0) x = 0;
            if (z < 0) z = 0;
            if (x >= cellsX) x = cellsX - 1;
            if (z >= cellsZ) z = cellsZ - 1;
            if (!blocked[z * cellsX + x]) return z * cellsX + x;

            for (int radius = 1; radius <= 10; radius++)
            {
                for (int dz = -radius; dz <= radius; dz++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        if (Math.Abs(dx) != radius && Math.Abs(dz) != radius) continue;
                        int nx = x + dx;
                        int nz = z + dz;
                        if (nx < 0 || nz < 0 || nx >= cellsX || nz >= cellsZ) continue;
                        int idx = nz * cellsX + nx;
                        if (!blocked[idx]) return idx;
                    }
                }
            }

            return -1;
        }

        static ExitField ComputeField(bool[] blocked, int cellsX, int cellsZ, int goal)
        {
            int count = blocked.Length;
            var dist = new float[count];
            var parent = new int[count];
            for (int i = 0; i < count; i++)
            {
                dist[i] = float.PositiveInfinity;
                parent[i] = -1;
            }

            if (goal < 0) return new ExitField(goal, dist, parent);

            dist[goal] = 0f;
            parent[goal] = goal;
            var heap = new MinHeap();
            heap.Push(goal, 0f);
            while (heap.Count > 0)
            {
                int node;
                float key;
                heap.Pop(out node, out key);
                if (key > dist[node]) continue;
                int x = node % cellsX;
                int z = node / cellsX;
                Relax(blocked, dist, parent, heap, cellsX, cellsZ, node, x + 1, z);
                Relax(blocked, dist, parent, heap, cellsX, cellsZ, node, x - 1, z);
                Relax(blocked, dist, parent, heap, cellsX, cellsZ, node, x, z + 1);
                Relax(blocked, dist, parent, heap, cellsX, cellsZ, node, x, z - 1);
            }

            return new ExitField(goal, dist, parent);
        }

        static void Relax(
            bool[] blocked,
            float[] dist,
            int[] parent,
            MinHeap heap,
            int cellsX,
            int cellsZ,
            int from,
            int x,
            int z)
        {
            if (x < 0 || z < 0 || x >= cellsX || z >= cellsZ) return;
            int index = z * cellsX + x;
            if (blocked[index]) return;
            float next = dist[from] + CellSize;
            if (next < dist[index])
            {
                dist[index] = next;
                parent[index] = from;
                heap.Push(index, next);
            }
        }

        sealed class ExitField
        {
            public readonly int Goal;
            public readonly float[] Dist;
            public readonly int[] Parent;

            public ExitField(int goal, float[] dist, int[] parent)
            {
                Goal = goal;
                Dist = dist;
                Parent = parent;
            }
        }

        sealed class MinHeap
        {
            readonly List<int> _nodes = new List<int>();
            readonly List<float> _keys = new List<float>();

            public int Count { get { return _nodes.Count; } }

            public void Push(int node, float key)
            {
                _nodes.Add(node);
                _keys.Add(key);
                int i = _nodes.Count - 1;
                while (i > 0)
                {
                    int parent = (i - 1) / 2;
                    if (_keys[parent] <= _keys[i]) break;
                    Swap(parent, i);
                    i = parent;
                }
            }

            public void Pop(out int node, out float key)
            {
                node = _nodes[0];
                key = _keys[0];
                int last = _nodes.Count - 1;
                _nodes[0] = _nodes[last];
                _keys[0] = _keys[last];
                _nodes.RemoveAt(last);
                _keys.RemoveAt(last);
                int i = 0;
                while (true)
                {
                    int left = i * 2 + 1;
                    if (left >= _nodes.Count) break;
                    int right = left + 1;
                    int smaller = left;
                    if (right < _nodes.Count && _keys[right] < _keys[left]) smaller = right;
                    if (_keys[i] <= _keys[smaller]) break;
                    Swap(i, smaller);
                    i = smaller;
                }
            }

            void Swap(int a, int b)
            {
                int node = _nodes[a];
                _nodes[a] = _nodes[b];
                _nodes[b] = node;
                float key = _keys[a];
                _keys[a] = _keys[b];
                _keys[b] = key;
            }
        }
    }
}
