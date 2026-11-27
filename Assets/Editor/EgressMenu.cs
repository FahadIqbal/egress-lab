using EgressLab.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EgressLab.EditorTools
{
    public static class EgressMenu
    {
        const string ScenePath = "Assets/Scenes/Evacuation.unity";

        [MenuItem("Egress Lab/Open Sample Scene")]
        public static void OpenSample()
        {
            EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Egress Lab/Create Bootstrap In Open Scene")]
        public static void CreateBootstrap()
        {
            var existing = Object.FindObjectOfType<EvacuationBootstrap>();
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                Debug.Log("Egress Lab bootstrap is already in this scene.");
                return;
            }

            var go = new GameObject("EvacuationBootstrap");
            go.AddComponent<EvacuationBootstrap>();
            Selection.activeGameObject = go;
        }
    }
}
