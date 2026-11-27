using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace EgressLab.Simulation
{
    /// <summary>
    /// Bakes a NavMesh from the physics colliders under the floor. Doing this at play time means the
    /// sample scene does not need a pre-baked asset: an empty scene plus the bootstrap is enough.
    /// </summary>
    public static class NavMeshBaker
    {
        public static BakeResult Bake(GameObject root)
        {
            Physics.SyncTransforms();
            var sources = new List<NavMeshBuildSource>();
            var markups = new List<NavMeshBuildMarkup>();
            var bounds = new Bounds(new Vector3(20f, 1.5f, 12f), new Vector3(48f, 8f, 32f));
            NavMeshBuilder.CollectSources(
                bounds,
                ~0,
                NavMeshCollectGeometry.PhysicsColliders,
                0,
                markups,
                sources);

            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = 0.28f;
            settings.agentHeight = 1.05f;
            settings.agentSlope = 40f;
            settings.agentClimb = 0.2f;

            NavMeshData data = NavMeshBuilder.BuildNavMeshData(
                settings,
                sources,
                bounds,
                Vector3.zero,
                Quaternion.identity);

            var result = new BakeResult();
            result.SourceCount = sources.Count;
            if (data != null) result.Instance = NavMesh.AddNavMeshData(data);
            if (!result.Instance.valid)
            {
                Debug.LogError("Egress Lab: NavMesh bake produced no surface. People will not walk. Sources found: " + sources.Count);
            }

            return result;
        }

        public struct BakeResult
        {
            public NavMeshDataInstance Instance;
            public int SourceCount;

            public bool Ok { get { return Instance.valid; } }
        }
    }
}
