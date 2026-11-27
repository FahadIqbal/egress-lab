using System;
using System.Collections.Generic;

namespace EgressLab.Decision
{
    public enum AgentStatus
    {
        Moving = 0,
        Evacuated = 1,
        Caught = 2
    }

    public enum RouteReason
    {
        ShortestWalk = 0,
        LowerCost = 1,
        KeptByHysteresis = 2
    }

    public enum SwitchMotive
    {
        None = 0,
        Distance = 1,
        Hazard = 2,
        Congestion = 3
    }

    public sealed class ExitState
    {
        public ExitState(string id, string displayName, WorldPos arrival, WorldPos approach, int softCapacity)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Exit id is required.", "id");
            Id = id;
            DisplayName = string.IsNullOrEmpty(displayName) ? id : displayName;
            Arrival = arrival;
            Approach = approach;
            SoftCapacity = softCapacity;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public WorldPos Arrival { get; }
        public WorldPos Approach { get; }
        public int SoftCapacity { get; }
        public bool Blocked { get; set; }
        public int ActiveCount { get; set; }
        public int EvacuatedCount { get; set; }
    }

    public sealed class AgentState
    {
        public AgentState(int id, WorldPos position, float baseSpeed)
        {
            Id = id;
            Position = position;
            BaseSpeed = baseSpeed;
            Status = AgentStatus.Moving;
        }

        public int Id { get; }
        public WorldPos Position { get; set; }
        public string ExitId { get; set; }
        public AgentStatus Status { get; set; }
        public float DwellInHazard { get; set; }
        public float BaseSpeed { get; }
    }

    /// <summary>
    /// Everything a policy is allowed to look at for one person. <see cref="LoadByExit"/> counts people
    /// already given that exit in the current planning pass, and does not include the person being scored.
    /// </summary>
    public sealed class RouteContext
    {
        public HazardField Hazard;
        public IReadOnlyList<ExitState> Exits;
        public FloorGraph Graph;
        public IReadOnlyDictionary<string, int> LoadByExit;
        public RoutingWeights Weights;
        public float LethalIntensity;
    }

    public readonly struct RouteChoice
    {
        public readonly string ExitId;
        public readonly RouteReason Reason;
        public readonly SwitchMotive Motive;
        public readonly float Cost;

        public RouteChoice(string exitId, RouteReason reason, SwitchMotive motive, float cost)
        {
            ExitId = exitId;
            Reason = reason;
            Motive = motive;
            Cost = cost;
        }
    }

    public sealed class EvacuationTally
    {
        public int Total { get; internal set; }
        public int Evacuated { get; internal set; }
        public int Caught { get; internal set; }
        public int Active { get; internal set; }
        public int PeakCongestion { get; internal set; }

        public bool ConservesPeople
        {
            get { return Evacuated + Caught + Active == Total; }
        }
    }

    public sealed class ReplanSummary
    {
        public int Rerouted { get; private set; }
        public int Held { get; private set; }
        public int HazardChoices { get; private set; }
        public int CongestionChoices { get; private set; }
        public int DistanceChoices { get; private set; }
        public string Headline { get; private set; }

        public static ReplanSummary Compose(
            RoutingMode mode,
            IReadOnlyList<ExitState> exits,
            int rerouted,
            int held,
            int hazardChoices,
            int congestionChoices,
            int distanceChoices)
        {
            var summary = new ReplanSummary();
            summary.Rerouted = rerouted;
            summary.Held = held;
            summary.HazardChoices = hazardChoices;
            summary.CongestionChoices = congestionChoices;
            summary.DistanceChoices = distanceChoices;
            summary.Headline = BuildHeadline(mode, exits, rerouted, held, hazardChoices, congestionChoices);
            return summary;
        }

        static string BuildHeadline(
            RoutingMode mode,
            IReadOnlyList<ExitState> exits,
            int rerouted,
            int held,
            int hazardChoices,
            int congestionChoices)
        {
            if (mode == RoutingMode.NearestExit)
                return "Nearest exit. Shortest walk only — hazard and congestion are ignored.";

            string counts = DescribeLoads(exits);
            int avoided = hazardChoices + congestionChoices;
            if (rerouted == 0 && held == 0)
            {
                if (avoided == 0)
                    return "Initial routes follow the shortest walk. " + counts;
                return "Initial routes. " + avoided + " people kept off a burning or crowded exit. " + counts;
            }

            if (rerouted == 0)
                return "Routes held. " + counts;

            return "Rerouted " + rerouted
                + " (" + hazardChoices + " hazard, " + congestionChoices + " congestion). "
                + counts;
        }

        static string DescribeLoads(IReadOnlyList<ExitState> exits)
        {
            if (exits == null || exits.Count == 0) return "No exits.";
            var parts = new string[exits.Count];
            for (int i = 0; i < exits.Count; i++)
            {
                ExitState exit = exits[i];
                parts[i] = exit.DisplayName + " " + exit.ActiveCount + (exit.Blocked ? " blocked" : "");
            }

            return string.Join(", ", parts) + ".";
        }
    }

    public sealed class RunSnapshot
    {
        public RoutingMode Mode { get; set; }
        public int Seed { get; set; }
        public int Agents { get; set; }
        public float SpreadRate { get; set; }
        public float Seconds { get; set; }
        public int Evacuated { get; set; }
        public int Caught { get; set; }
        public int Remaining { get; set; }
        public bool HitTimeLimit { get; set; }
        public int PeakCongestion { get; set; }
    }

    public static class CrowdMotion
    {
        /// <summary>
        /// Soft bottleneck. At or under capacity, people keep their own speed. Over capacity, the
        /// assigned group slows. This is a queue proxy, not a social-force model.
        /// </summary>
        public static float Adjust(float baseSpeed, int assigned, int capacity)
        {
            if (baseSpeed < 0f) baseSpeed = 0f;
            if (capacity <= 0 || assigned <= capacity) return baseSpeed;
            float extra = (assigned - capacity) / (float)capacity;
            return baseSpeed / (1f + 0.5f * extra);
        }
    }

    public static class SimText
    {
        public static string Clock(float seconds)
        {
            if (seconds < 0f || float.IsNaN(seconds) || float.IsInfinity(seconds)) seconds = 0f;
            int whole = (int)seconds;
            int minutes = whole / 60;
            int secs = whole % 60;
            int tenth = (int)((seconds - whole) * 10f);
            if (tenth < 0) tenth = 0;
            if (tenth > 9) tenth = 9;
            return minutes.ToString("00") + ":" + secs.ToString("00") + "." + tenth.ToString();
        }
    }
}
