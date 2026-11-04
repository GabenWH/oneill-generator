using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Grace.VWaterOriginal.Editor
{
    /// <summary>Replaces only the test drone; leaves scene saving to the user.</summary>
    [InitializeOnLoad]
    public static class GraceUpdatedDroneInstaller
    {
        private const string Folder="Assets/GraceVWaterOriginalTest/UpdatedDrone50";
        private const string TargetScene="Assets/GraceVWaterOriginalTestRuns/LargePond_20261103/VWater_Drone_Large_Pond.unity";
        private const string UpdatedName="Drone50Updated";
        private const string Version="e724a0b6f7733344c4968b8688d5df9c734d34a14c0bb5a5ff5b1dca987b8ae4";
        [Serializable] private sealed class MaterialRecord { public string name; }
        [Serializable] private sealed class Manifest { public int meshObjects;public MaterialRecord[] materials; }

        static GraceUpdatedDroneInstaller()
        {
            Queue();
            EditorSceneManager.sceneOpened+=(scene,mode)=>Queue();
            EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredEditMode)Queue();};
        }

        private static void Queue()
        { EditorApplication.delayCall-=TryAutomatic;EditorApplication.delayCall+=TryAutomatic; }

        private static string SessionKey(Scene scene) { return "GraceUpdatedDrone/"+Version+"/"+scene.handle; }

        private static void TryAutomatic()
        {
            if(EditorApplication.isCompiling || EditorApplication.isUpdating){Queue();return;}
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            Scene scene=SceneManager.GetActiveScene();
            if(scene.path!=TargetScene || SessionState.GetBool(SessionKey(scene),false))return;
            // One attempt per loaded scene/session also lets an intentional Undo stay undone.
            SessionState.SetBool(SessionKey(scene),true);
            Install(scene);
        }

        [MenuItem("Tools/Grace/VWater/Use Updated 50-Wide Drone")]
        public static void InstallInActiveScene()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)
            { Debug.LogWarning("Stop Play before updating the test drone.");return; }
            Scene scene=SceneManager.GetActiveScene();
            if(scene.path!=TargetScene)
            { Debug.LogWarning("Open the large pond test before updating its drone.");return; }
            SessionState.SetBool(SessionKey(scene),true);
            Install(scene);
        }

        private static Transform Find(Transform root,string name)
        { return root.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name); }
        private static void Require(bool condition,string message)
        { if(!condition)throw new InvalidOperationException(message); }
        private static Quaternion Basis(Transform root)
        {
            Vector3 origin=Find(root,"GraceRig_Origin").position;
            Vector3 y=Find(root,"GraceRig_Y").position-origin,z=Find(root,"GraceRig_Z").position-origin;
            Require(y.magnitude>.0001f && z.magnitude>.0001f && Mathf.Abs(Vector3.Dot(y.normalized,z.normalized))<.001f,"Drone axis markers are invalid.");
            return Quaternion.LookRotation(z.normalized,y.normalized);
        }

        private static void Install(Scene scene)
        {
            GraceOriginalVWaterFanBridge bridge=null;
            GameObject replacement=null;
            int undo=-1;
            try
            {
                var bridges=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<GraceOriginalVWaterFanBridge>(true)).ToArray();
                Require(bridges.Length==1,"Expected exactly one test bridge; no scene objects were replaced.");
                bridge=bridges[0];
                Require(bridge.dronePlacement!=null,"Existing drone placement is missing.");
                if(bridge.dronePlacement.Find(UpdatedName)!=null)return;
                Transform old=bridge.dronePlacement.Find("Drone50Wide");
                Require(old!=null,"Existing drone not found; no scene objects were replaced.");
                var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/Drone50Updated.fbx");
                var json=AssetDatabase.LoadAssetAtPath<TextAsset>(Folder+"/DroneUpdatedManifest.json");
                Require(asset!=null && json!=null,"Updated drone assets are not imported. Refresh Assets, then use Tools > Grace > VWater > Use Updated 50-Wide Drone.");
                var manifest=JsonUtility.FromJson<Manifest>(json.text);
                Require(manifest!=null && manifest.materials!=null && manifest.meshObjects>0,"Updated drone manifest is invalid.");
                var materials=manifest.materials.ToDictionary(m=>m.name,m=>AssetDatabase.LoadAssetAtPath<Material>(Folder+"/"+m.name+".mat"));
                Require(materials.Values.All(m=>m!=null),"An updated drone material is missing.");
                Require(bridge.fanHubs.Length==2 && bridge.fanHubs.All(h=>h!=null),"Existing fan bindings are missing.");
                Vector3[] oldFanPositions=bridge.fanHubs.Select(h=>h.position).OrderBy(p=>p.x).ToArray();
                Vector3 oldOrigin=Find(old,"GraceRig_Origin").position;
                Quaternion oldBasis=Basis(old);
                float oldUnit=(Find(old,"GraceRig_Y").position-oldOrigin).magnitude;

                Undo.IncrementCurrentGroup();undo=Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Use updated 50-wide drone");
                replacement=(GameObject)PrefabUtility.InstantiatePrefab(asset,bridge.dronePlacement);
                Undo.RegisterCreatedObjectUndo(replacement,"Create updated drone");
                PrefabUtility.UnpackPrefabInstance(replacement,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                Undo.RecordObject(replacement.transform,"Align updated drone");
                replacement.name=UpdatedName;
                Transform root=replacement.transform;
                Vector3 newOrigin=Find(root,"GraceRig_Origin").position;
                float newUnit=(Find(root,"GraceRig_Y").position-newOrigin).magnitude;
                Quaternion newBasis=Basis(root);
                root.rotation=oldBasis*Quaternion.Inverse(newBasis)*root.rotation;
                root.localScale*=oldUnit/newUnit;
                root.position+=oldOrigin-Find(root,"GraceRig_Origin").position;
                var all=root.GetComponentsInChildren<Transform>(true);
                Transform[] hubs=all.Where(t=>t.name.StartsWith("Fan hub",StringComparison.Ordinal)).OrderBy(t=>t.position.x).ToArray();
                Transform[] blades=all.Where(t=>t.name.StartsWith("Low-poly fan blade",StringComparison.Ordinal)).ToArray();
                Require(hubs.Length==2 && blades.Length==10,"Updated actual fan rig is incomplete.");
                var up=hubs.Select(h=>h.GetComponentsInChildren<Transform>(true).Single(t=>t.name.StartsWith("GraceFanUp_",StringComparison.Ordinal))).ToArray();
                for(int fan=0;fan<2;fan++)
                {
                    Require(Vector3.Distance(hubs[fan].position,oldFanPositions[fan])<.003f,"Updated fan position changed unexpectedly.");
                    Require(Vector3.Dot((up[fan].position-hubs[fan].position).normalized,Vector3.up)>.999f,"Updated fan axis is not vertical.");
                }
                Require(root.GetComponentsInChildren<MeshFilter>(true).Length==manifest.meshObjects,"Unity import lost updated mesh parts.");
                foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    Undo.RecordObject(renderer,"Bind updated drone materials");
                    renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>
                    {
                        Material mapped;
                        Require(m!=null && materials.TryGetValue(m.name,out mapped),"Updated FBX material slot is unmapped.");
                        return materials[m.name];
                    }).ToArray();
                }
                Undo.RegisterCompleteObjectUndo(bridge,"Rebind updated fans");
                bridge.fanHubs=hubs;bridge.fanUpMarkers=up;bridge.fanBlades=blades;
                Require(bridge.ValidateBindings(out string problem),problem);
                Undo.RegisterCompleteObjectUndo(old.gameObject,"Retain original drone disabled");
                old.gameObject.SetActive(false);
                EditorUtility.SetDirty(bridge);
                EditorSceneManager.MarkSceneDirty(scene);
                Undo.FlushUndoRecordObjects();
                Undo.CollapseUndoOperations(undo);
                Debug.Log("[Updated drone] Installed updated 50-wide hull, ingress hatch and cockpit; "+manifest.meshObjects+" mesh parts, two actual fan hubs and ten blades. Original drone retained disabled. Profile, water, camera and wave settings preserved. Save the scene to keep this swap; Undo reverses it.",bridge);
            }
            catch(Exception error)
            {
                if(undo>=0){Undo.FlushUndoRecordObjects();Undo.RevertAllDownToGroup(undo);}
                if(replacement!=null)UnityEngine.Object.DestroyImmediate(replacement);
                Debug.LogError("[Updated drone] Update cancelled; original model retained. "+error.Message,bridge);
            }
        }
    }
}
