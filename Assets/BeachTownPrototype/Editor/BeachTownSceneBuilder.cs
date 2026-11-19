using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CylinderBeachTown
{
    internal static class BeachTownSceneBuilder
    {
        private const string Folder = "Assets/BeachTownPrototype";
        [MenuItem("Tools/O'Neill/Create Beach Town Prototype")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            EditorSceneManager.SetActiveScene(scene);
            var town = new GameObject("Harbor Beach — runtime town").AddComponent<BeachTownPrototype>();
            town.harborLocalModel = AssetDatabase.LoadAssetAtPath<GameObject>(
                Folder + "/Models/T01_Harbor_Local.fbx");
            string path = AssetDatabase.GenerateUniqueAssetPath(Folder + "/HarborBeach.unity");
            if (!EditorSceneManager.SaveScene(scene, path))
            {
                Debug.LogError("Beach Town scene could not be saved.");
                return;
            }
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            Debug.Log("Harbor Beach created at " + path + ". Press Play to explore.");
        }
    }
}
