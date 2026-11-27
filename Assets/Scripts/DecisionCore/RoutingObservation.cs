using System;
using System.Collections.Generic;

namespace EgressLab.Decision
{
    /// <summary>
    /// Fixed-length observation vector for an optional ML-Agents policy. The project does not train
    /// anything: <see cref="DecisionSupportPolicy"/> is the policy that actually runs. This encoder is
    /// the contract a future Agent.CollectObservations would write, and it is covered by tests.
    /// Layout: 5x5 local hazard, then per exit (dx, dz, normalised distance, exposure, congestion, blocked),
    /// then own speed and seconds already spent in a lethal cell.
    /// </summary>
    public static class RoutingObservation
    {
        public const int Window = 5;
        public const int PerExit = 6;
        public const int SelfFeatures = 2;

        public static int Size(int exitCount)
        {
            if (exitCount < 0) throw new ArgumentOutOfRangeException("exitCount");
            return Window * Window + exitCount * PerExit + SelfFeatures;
        }

        public static void Write(
            float[] destination,
            AgentState agent,
            HazardField hazard,
            IReadOnlyList<ExitState> exits,
            FloorGraph graph,
            IReadOnlyDictionary<string, int> loadByExit)
        {
            if (destination == null) throw new ArgumentNullException("destination");
            if (agent == null) throw new ArgumentNullException("agent");
            if (hazard == null) throw new ArgumentNullException("hazard");
            if (exits == null) throw new ArgumentNullException("exits");
            if (graph == null) throw new ArgumentNullException("graph");

            int expected = Size(exits.Count);
            if (destination.Length < expected)
                throw new ArgumentException("Observation buffer is shorter than RoutingObservation.Size.", "destination");

            int cursor = 0;
            int half = Window / 2;
            int originX = (int)agent.Position.X;
            int originZ = (int)agent.Position.Z;
            for (int dz = -half; dz <= half; dz++)
            {
                for (int dx = -half; dx <= half; dx++)
                {
                    int x = originX + dx;
                    int z = originZ + dz;
                    float value = 0f;
                    if (x >= 0 && z >= 0 && x < hazard.Width && z < hazard.Depth) value = hazard[x, z];
                    destination[cursor++] = value;
                }
            }

            for (int i = 0; i < exits.Count; i++)
            {
                ExitState exit = exits[i];
                float dx = (exit.Arrival.X - agent.Position.X) / BuildingLayout.Width;
                float dz = (exit.Arrival.Z - agent.Position.Z) / BuildingLayout.Depth;
                float distance = graph.Distance(exit.Id, agent.Position);
                float norm = float.IsInfinity(distance) ? 1f : distance / BuildingLayout.Diagonal;
                if (norm > 1f) norm = 1f;
                if (norm < 0f) norm = 0f;
                float mean;
                float max;
                graph.SamplePathHazard(exit.Id, agent.Position, exit.Arrival, hazard, out mean, out max);
                float exposure = 0.35f * mean + 0.65f * max;
                int load = 0;
                if (loadByExit != null) loadByExit.TryGetValue(exit.Id, out load);
                float congestion = exit.SoftCapacity <= 0 ? 0f : load / (float)exit.SoftCapacity;
                destination[cursor++] = dx;
                destination[cursor++] = dz;
                destination[cursor++] = norm;
                destination[cursor++] = exposure;
                destination[cursor++] = congestion;
                destination[cursor++] = exit.Blocked ? 1f : 0f;
            }

            destination[cursor++] = agent.BaseSpeed / 3f;
            destination[cursor++] = agent.DwellInHazard;
        }
    }
}
