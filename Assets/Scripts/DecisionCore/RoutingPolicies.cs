using System;
using System.Collections.Generic;

namespace EgressLab.Decision
{
    public interface IRoutingPolicy
    {
        RouteChoice Choose(AgentState agent, RouteContext context);
    }

    public static class RoutingPolicies
    {
        public static IRoutingPolicy Create(RoutingMode mode)
        {
            if (mode == RoutingMode.DecisionSupport) return new DecisionSupportPolicy();
            return new NearestExitPolicy();
        }
    }

    /// <summary>
    /// Baseline. Shortest walk on the floor graph. Fire, smoke, and queues are invisible to it,
    /// including a door that is already blocked.
    /// </summary>
    public sealed class NearestExitPolicy : IRoutingPolicy
    {
        public RouteChoice Choose(AgentState agent, RouteContext context)
        {
            if (agent == null) throw new ArgumentNullException("agent");
            if (context == null) throw new ArgumentNullException("context");
            string bestId = null;
            float best = float.PositiveInfinity;
            IReadOnlyList<ExitState> exits = context.Exits;
            if (exits == null) return new RouteChoice(null, RouteReason.ShortestWalk, SwitchMotive.None, float.PositiveInfinity);

            for (int i = 0; i < exits.Count; i++)
            {
                float distance = context.Graph.Distance(exits[i].Id, agent.Position);
                if (distance < best)
                {
                    best = distance;
                    bestId = exits[i].Id;
                }
            }

            return new RouteChoice(bestId, RouteReason.ShortestWalk, SwitchMotive.Distance, best);
        }
    }

    /// <summary>
    /// Utility router. For each door the cost is a weighted sum of walk length, hazard along that
    /// walk, queue length, a penalty if the door itself is burning, and a further penalty if the
    /// walk crosses a lethal cell. A small hysteresis margin stops people oscillating between
    /// near-equal doors. A blocked door does not get that margin: they are allowed to leave it.
    /// </summary>
    public sealed class DecisionSupportPolicy : IRoutingPolicy
    {
        public RouteChoice Choose(AgentState agent, RouteContext context)
        {
            if (agent == null) throw new ArgumentNullException("agent");
            if (context == null) throw new ArgumentNullException("context");
            IReadOnlyList<ExitState> exits = context.Exits;
            if (exits == null || exits.Count == 0)
                return new RouteChoice(null, RouteReason.LowerCost, SwitchMotive.None, float.PositiveInfinity);

            RoutingWeights weights = context.Weights ?? new RoutingWeights();
            var scores = new Score[exits.Count];
            int bestIndex = 0;
            int shortestIndex = 0;

            for (int i = 0; i < exits.Count; i++)
            {
                scores[i] = ScoreExit(agent, exits[i], context, weights);
                if (scores[i].Distance < scores[shortestIndex].Distance) shortestIndex = i;
                if (scores[i].Total < scores[bestIndex].Total) bestIndex = i;
            }

            int chosen = bestIndex;
            RouteReason reason = RouteReason.LowerCost;
            SwitchMotive motive = MotiveFor(scores, chosen, shortestIndex);

            if (!string.IsNullOrEmpty(agent.ExitId))
            {
                int current = IndexOf(exits, agent.ExitId);
                if (current >= 0 && current != chosen && !exits[current].Blocked)
                {
                    float gain = scores[current].Total - scores[chosen].Total;
                    if (gain < weights.Hysteresis)
                    {
                        chosen = current;
                        reason = RouteReason.KeptByHysteresis;
                        motive = SwitchMotive.None;
                    }
                }
            }

            if (chosen == shortestIndex && reason != RouteReason.KeptByHysteresis)
                motive = SwitchMotive.Distance;

            return new RouteChoice(exits[chosen].Id, reason, motive, scores[chosen].Total);
        }

        static SwitchMotive MotiveFor(Score[] scores, int chosen, int shortest)
        {
            if (chosen == shortest) return SwitchMotive.Distance;
            float hazardSave =
                (scores[shortest].ExposureCost + scores[shortest].Block + scores[shortest].Lethal)
                - (scores[chosen].ExposureCost + scores[chosen].Block + scores[chosen].Lethal);
            float congestionSave = scores[shortest].CongestionCost - scores[chosen].CongestionCost;
            if (hazardSave <= 0f && congestionSave <= 0f) return SwitchMotive.Distance;
            if (hazardSave >= congestionSave) return SwitchMotive.Hazard;
            return SwitchMotive.Congestion;
        }

        static Score ScoreExit(AgentState agent, ExitState exit, RouteContext context, RoutingWeights weights)
        {
            float distance = context.Graph.Distance(exit.Id, agent.Position);
            float normDistance = float.IsInfinity(distance) ? 20f : distance / BuildingLayout.Diagonal;
            float mean;
            float max;
            context.Graph.SamplePathHazard(exit.Id, agent.Position, exit.Arrival, context.Hazard, out mean, out max);
            float exposure = 0.35f * mean + 0.65f * max;
            int load = 0;
            if (context.LoadByExit != null) context.LoadByExit.TryGetValue(exit.Id, out load);
            if (load < 0) load = 0;
            float congestion = exit.SoftCapacity <= 0 ? 0f : load / (float)exit.SoftCapacity;

            var score = new Score();
            score.Distance = float.IsInfinity(distance) ? 1e6f : distance;
            score.DistanceCost = weights.Distance * normDistance;
            score.ExposureCost = weights.Exposure * exposure;
            score.CongestionCost = weights.Congestion * congestion;
            score.Block = exit.Blocked ? weights.BlockedExit : 0f;
            score.Lethal = max >= context.LethalIntensity ? weights.LethalCorridor : 0f;
            score.Total = score.DistanceCost + score.ExposureCost + score.CongestionCost + score.Block + score.Lethal;
            return score;
        }

        static int IndexOf(IReadOnlyList<ExitState> exits, string id)
        {
            for (int i = 0; i < exits.Count; i++)
            {
                if (exits[i].Id == id) return i;
            }

            return -1;
        }

        struct Score
        {
            public float Distance;
            public float DistanceCost;
            public float ExposureCost;
            public float CongestionCost;
            public float Block;
            public float Lethal;
            public float Total;
        }
    }
}
