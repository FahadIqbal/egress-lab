using System;
using System.Globalization;
using EgressLab.Decision;

namespace EgressLab.Headless
{
    /// <summary>
    /// Prints a straight-walk drill of the same policies the Unity scene uses.
    /// People follow the floor graph here. In the Editor they follow a NavMesh, so the seconds will differ.
    /// </summary>
    public static class Program
    {
        public static int Main()
        {
            int[] seeds = { 7, 42, 99 };
            Console.WriteLine("mode,seed,agents,spread,evacuated,caught,remaining,seconds,peak");
            for (int s = 0; s < seeds.Length; s++)
            {
                Report(seeds[s], RoutingMode.NearestExit);
                Report(seeds[s], RoutingMode.DecisionSupport);
            }

            EvacuationSession sample = EvacuationSession.Start(new SimulationConfig
            {
                Seed = 42,
                AgentCount = 40,
                SpreadRate = 1f,
                Mode = RoutingMode.DecisionSupport
            });
            Console.WriteLine();
            Console.WriteLine(sample.LastReplan.Headline);
            return 0;
        }

        static void Report(int seed, RoutingMode mode)
        {
            var config = new SimulationConfig
            {
                Seed = seed,
                AgentCount = 40,
                SpreadRate = 1f,
                Mode = mode
            };
            EvacuationSession session = EvacuationSession.Start(config);
            int guard = 0;
            while (!session.IsComplete && guard++ < 5000) session.Tick(0.1f, true);
            RunSnapshot snap = session.CreateSnapshot();
            Console.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "{0},{1},{2},{3:0.0},{4},{5},{6},{7:0.0},{8}",
                snap.Mode,
                snap.Seed,
                snap.Agents,
                snap.SpreadRate,
                snap.Evacuated,
                snap.Caught,
                snap.Remaining,
                snap.Seconds,
                snap.PeakCongestion));
        }
    }
}
