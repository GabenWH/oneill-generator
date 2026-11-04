using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Vertigo2;

namespace Grace.VWaterOriginal.Editor
{
    /// <summary>User-invoked NEW-scene wiring. No import/startup execution hook.</summary>
    public static class GraceOriginalVWaterSceneBuilder
    {
        private const string Demo="Assets/GraceVWaterOriginalTest";
        private const string Runs="Assets/GraceVWaterOriginalTestRuns";
        private const string WaterPrefabGuid="f7d09682866196346b7d91bbf6b27c29";
        private const string ManagerPrefabGuid="d29a0edaa689517438449c157e721a37";
        private const string SurfaceGuid="a0dea42ebbe424e8293f6bd6b3fd0e08";
        [Serializable] private class RequiredAsset { public string guid; public string sha256; public string sourcePath; }
        [Serializable] private class RequiredAssets { public RequiredAsset[] assets; }
        [Serializable] private class DroneMaterial { public string name; public float[] color; public float metallic; public float roughness; public bool alphaBlend; public string texture; }
        [Serializable] private class DroneMaterials { public DroneMaterial[] materials; }

        [MenuItem("Tools/Grace/VWater/Create Skimming Drone Test")]
        public static void Create()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode){Debug.LogError("Stop Play first. No scene changed.");return;}
            if(GraphicsSettings.currentRenderPipeline!=null){Debug.LogError("Original VWater requires Built-in, not URP/HDRP. No scene changed.");return;}
            if(EditorSettings.enterPlayModeOptionsEnabled&&(EditorSettings.enterPlayModeOptions&EnterPlayModeOptions.DisableDomainReload)!=0)
            {Debug.LogError("For this original baseline use normal domain reload in a disposable project. Settings were NOT changed.");return;}
            GameObject sourceWater=Load<GameObject>(WaterPrefabGuid),sourceManager=Load<GameObject>(ManagerPrefabGuid);
            if(sourceWater==null||sourceManager==null){Debug.LogError("Import this adapted VWater test package first. No scene changed.");return;}
            try{ValidateSupportingSources();}catch(Exception e){Debug.LogError("Supporting-asset preflight: "+e.Message+" No files overwritten; reconcile this installation first.");return;}
            if(!SystemInfo.supportsComputeShaders||!SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)||!SystemInfo.SupportsRandomWriteOnRenderTextureFormat(RenderTextureFormat.ARGBHalf))
            {Debug.LogError("Original GPU ripple test requires compute/RW ARGBHalf on this rig. No substitute solver or fallback scene was created.");return;}
            if(!EditorUtility.DisplayDialog("NEW adapted VWater skimming test?",
                "Uses the original water renderer and textures with an adapted compute simulation. Creates a NEW flat-world-Y scene and NEW scene-owned materials, after Unity's save/cancel prompt. " +
                "Water never rotates. Actual drone fans drive the adapted skim input. No Unity authoring/Play/GPU validation was possible on the author's executor. Use a disposable 2022.3.62f3 Built-in project.","Create NEW scene","Cancel"))return;
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            string folder=NewFolder();
            try
            {
                // Original ExecuteInEditMode renderer must NEVER see vendor
                // shared materials before they are cloned. Inactive parent is
                // essential: no OnEnable/Start until ALL references are ready.
                GameObject stage=new GameObject("Adapted VWater skimming test — stationary world-Y");stage.SetActive(false);
                GameObject managers=InstantiateScene(sourceManager,stage.transform);managers.name="VWater manager pair (adapted dynamics)";
                VWaterDynamicsManager dynamics=managers.GetComponent<VWaterDynamicsManager>();
                VWaterManager manager=managers.GetComponent<VWaterManager>();
                Require(dynamics!=null&&manager!=null,"Original manager prefab components missing");
                Require(dynamics.compute!=null&&dynamics.compute.HasKernel("CSWater")&&dynamics.compute.HasKernel("CSOffsetBuffers"),"Original CSWater kernel not imported/compiled on this rig");
                manager.dynamicsManager=dynamics;
                // More pixels across the 6 m pond while keeping the orbit camera in range.
                dynamics.universeSize=24f; dynamics.followCameraGridSize=.5f;

                GameObject pond=InstantiateScene(sourceWater,stage.transform);pond.name="Original VWater — FLAT 6m test pond";
                pond.transform.localPosition=Vector3.zero;pond.transform.localRotation=Quaternion.identity;pond.transform.localScale=new Vector3(.6f,1,.6f);
                VWater water=pond.GetComponent<VWater>();VWaterRenderer renderer=pond.GetComponent<VWaterRenderer>();MeshRenderer meshRenderer=pond.GetComponent<MeshRenderer>();
                Require(water!=null&&renderer!=null&&meshRenderer!=null,"Original water prefab components missing");
                var copies=new Material[meshRenderer.sharedMaterials.Length];
                for(int i=0;i<copies.Length;i++)copies[i]=Clone(meshRenderer.sharedMaterials[i],folder,"Original water material "+i);
                meshRenderer.sharedMaterials=copies;
                Require(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(copies[0].shader))==SurfaceGuid,"Not the ORIGINAL surface shader");
                Require(copies[0].GetTexture("_Normal")!=null&&copies[0].GetTexture("_FoamMap")!=null,"Original normal/foam texture dependencies missing");
                if(renderer.overlayMaterial!=null)renderer.overlayMaterial=Clone(renderer.overlayMaterial,folder,"Original overlay (unused above water)");
                renderer.linkedWater=water;water.linkedRenderer=renderer;renderer.drawWaterOverlay=false;renderer.testInEditor=false;

                Shader standard=Shader.Find("Standard");Require(standard!=null,"Built-in Standard shader missing");
                Material soil=NewStandard(standard,folder,"Opaque test basin",new Color(.24f,.19f,.12f),0,.2f);
                Mesh basin=Basin();AssetDatabase.CreateAsset(basin,folder+"/Basin.asset");
                GameObject bottom=Child("Opaque 0→1.2m basin / original optical depth input",stage.transform);bottom.AddComponent<MeshFilter>().sharedMesh=basin;bottom.AddComponent<MeshRenderer>().sharedMaterial=soil;
                GameObject sun=Child("Sun",stage.transform);sun.transform.rotation=Quaternion.Euler(48,-30,0);Light light=sun.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.1f;light.shadows=LightShadows.Soft;renderer.sunLight=light;
                RenderSettings.sun=light;RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Skybox;
                Shader skyShader=Shader.Find("Skybox/Procedural");if(skyShader!=null){var sky=new Material(skyShader){name="New test sky"};AssetDatabase.CreateAsset(sky,folder+"/Sky.mat");RenderSettings.skybox=sky;}
                GameObject cameraObject=Child("Main Camera",stage.transform);cameraObject.tag="MainCamera";cameraObject.transform.position=new Vector3(4.5f,3.6f,-6.2f);cameraObject.transform.LookAt(Vector3.zero,Vector3.up);
                Camera camera=cameraObject.AddComponent<Camera>();camera.fieldOfView=48;camera.nearClipPlane=.05f;camera.farClipPlane=100;camera.depthTextureMode|=DepthTextureMode.Depth;camera.clearFlags=CameraClearFlags.Skybox;cameraObject.AddComponent<AudioListener>();
                GraceOriginalVWaterCameraOrbit orbit=cameraObject.AddComponent<GraceOriginalVWaterCameraOrbit>();orbit.target=pond.transform;orbit.autoOrbit=false;
                GameObject probeObject=Child("Scene-owned original reflection input",stage.transform);probeObject.transform.position=new Vector3(0,1,0);ReflectionProbe probe=probeObject.AddComponent<ReflectionProbe>();
                probe.mode=ReflectionProbeMode.Realtime;probe.refreshMode=ReflectionProbeRefreshMode.OnAwake;probe.resolution=128;probe.size=new Vector3(16,12,16);probe.boxProjection=true;probe.cullingMask=~(1<<4);

                GameObject drone=PrepareDrone(stage.transform,folder,standard,out Transform[] hubs,out Transform[] up,out Transform[] blades);
                Vector3 hubCenter=(hubs[0].position+hubs[1].position)*.5f;drone.transform.position+=new Vector3(0,1.9f,0)-hubCenter;
                GraceOriginalVWaterFanBridge bridge=stage.AddComponent<GraceOriginalVWaterFanBridge>();bridge.water=water;bridge.originalRenderer=renderer;bridge.originalDynamics=dynamics;bridge.dronePlacement=drone.transform;bridge.fanHubs=hubs;bridge.fanUpMarkers=up;bridge.fanBlades=blades;bridge.cameraOrbit=orbit;bridge.fansEnabled=false;bridge.moveDrone=false;
                bridge.sprayShader=AssetDatabase.LoadAssetAtPath<Shader>(Demo+"/Shaders/GraceDownwashMist.shader");
                bridge.waveShader=AssetDatabase.LoadAssetAtPath<Shader>(Demo+"/Shaders/GraceVisibleWater.shader");
                bridge.responseProfile=AssetDatabase.LoadAssetAtPath<DownwashResponseProfile>(Demo+"/Profiles/DroneHeightResponse.asset");
                Require(bridge.ValidateBindings(out string problem),problem);
                stage.SetActive(true);
                foreach(UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(folder+"/Basin.asset"))AssetDatabase.SaveAssetIfDirty(asset);
                string path=AssetDatabase.GenerateUniqueAssetPath(folder+"/VWater_Drone_Skimming_Test.unity");
                Require(EditorSceneManager.SaveScene(scene,path),"New scene save failed; save this NEW scene manually");
                Selection.activeGameObject=pond;
                Debug.Log("Created "+path+". Adapted VWater compute and manager; original surface renderer retained. No water rotation. Press Play; first inspect stationary original water, then Fans ON → Move → Raise. This author did NOT run Unity.");
            }
            catch(Exception e){Debug.LogException(e);Debug.LogError("Stopped. Only partial NEW scene/assets at "+folder+"; original source/assets were not overwritten.");}
        }
        private static void ValidateSupportingSources()
        {
            TextAsset json=AssetDatabase.LoadAssetAtPath<TextAsset>(Demo+"/Editor/OriginalRequiredAssets.json");Require(json!=null,"Missing original-source manifest");
            RequiredAssets required=JsonUtility.FromJson<RequiredAssets>(json.text);
            foreach(RequiredAsset asset in required.assets)
            {
                // Deliberate simulation adaptations, plus the renderer's pre-draw
                // buffer-coordinate refresh. Other vendor dependencies remain pinned.
                if(asset.sourcePath=="Assets/VWater/Shaders/VWaterCompute.compute" ||
                   asset.sourcePath=="Assets/VWater/Scripts/VWaterDynamicsManager.cs" ||
                   asset.sourcePath=="Assets/VWater/Scripts/VWaterRenderer.cs") continue;
                string path=AssetDatabase.GUIDToAssetPath(asset.guid);Require(!string.IsNullOrEmpty(path),"Missing original asset "+asset.sourcePath);
                byte[] bytes=File.ReadAllBytes(Path.GetFullPath(Path.Combine(Application.dataPath,"..",path)));
                using(SHA256 hash=SHA256.Create()){string actual=BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();Require(actual==asset.sha256,"Not byte-exact original: "+path);}
            }
        }
        private static GameObject PrepareDrone(Transform parent,string folder,Shader standard,out Transform[] hubs,out Transform[] up,out Transform[] blades)
        {
            string path=Demo+"/PreparedDrone/Drone50Wide.fbx";ModelImporter importer=AssetImporter.GetAtPath(path) as ModelImporter;Require(importer!=null,"Prepared FBX missing; Unity has no native GLB importer");
            importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;importer.preserveHierarchy=true;importer.importAnimation=false;importer.globalScale=1;importer.SaveAndReimport();
            GameObject placement=Child("Actual approved 50-WIDE drone placement",parent);
            GameObject model=InstantiateScene(AssetDatabase.LoadAssetAtPath<GameObject>(path),placement.transform);
            Transform[] all=model.GetComponentsInChildren<Transform>(true);
            Func<string,Transform> find=name=>all.Single(t=>t.name==name);
            Transform origin=find("GraceRig_Origin");Vector3 y=find("GraceRig_Y").position-origin.position,z=find("GraceRig_Z").position-origin.position;
            float unit=y.magnitude;Require(unit>.0001f&&unit<1000&&Mathf.Abs(Vector3.Dot(y.normalized,z.normalized))<.001f,"FBX axes/units invalid");
            placement.transform.rotation=Quaternion.Inverse(Quaternion.LookRotation(z.normalized,y.normalized));placement.transform.localScale=Vector3.one/unit;placement.transform.position-=origin.position;
            hubs=all.Where(t=>Normalized(t.name).StartsWith("fanhub",StringComparison.Ordinal)).OrderBy(t=>t.position.x).ToArray();
            blades=all.Where(t=>Normalized(t.name).StartsWith("lowpolyfanblade",StringComparison.Ordinal)).ToArray();
            Require(hubs.Length==2&&blades.Length==10,"Actual imported fan rig missing; no proxy fallback");
            up=hubs.Select(h=>h.GetComponentsInChildren<Transform>(true).Single(t=>t.name.StartsWith("GraceFanUp_",StringComparison.Ordinal))).ToArray();
            for(int i=0;i<2;i++){Require(Mathf.Abs(Mathf.Abs(hubs[i].position.x)-.54f)<.003f&&Mathf.Abs(hubs[i].position.y-.96f)<.003f&&Mathf.Abs(hubs[i].position.z-.85f)<.003f,"Unity FBX hub positions do not match approved source");Require(Vector3.Dot((up[i].position-hubs[i].position).normalized,Vector3.up)>.999f,"Imported actual fan axis is not source +Y");}
            DroneMaterials description=JsonUtility.FromJson<DroneMaterials>(AssetDatabase.LoadAssetAtPath<TextAsset>(Demo+"/PreparedDrone/DroneMaterials.json").text);
            var materials=new Dictionary<string,Material>();
            foreach(DroneMaterial record in description.materials)
            {
                float[] c=record.color;Material material=NewStandard(standard,folder,record.name,new Color(c[0],c[1],c[2],c[3]),record.metallic,1-record.roughness);
                if(!string.IsNullOrEmpty(record.texture))material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Demo+"/PreparedDrone/"+record.texture);
                if(record.alphaBlend){material.SetFloat("_Mode",3);material.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);material.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);material.SetInt("_ZWrite",0);material.EnableKeyword("_ALPHABLEND_ON");material.renderQueue=3000;}
                AssetDatabase.SaveAssetIfDirty(material);materials.Add(record.name,material);
            }
            foreach(Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                Material[] slots=renderer.sharedMaterials;for(int i=0;i<slots.Length;i++){Require(slots[i]!=null,"FBX material slot missing");Material converted;Require(materials.TryGetValue(slots[i].name,out converted),"FBX material name mapping missing (no silent substitute)");slots[i]=converted;}renderer.sharedMaterials=slots;
            }
            return placement;
        }
        private static string Normalized(string name){return new string(name.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());}
        private static GameObject InstantiateScene(GameObject prefab,Transform parent)
        {Require(prefab!=null,"Missing source prefab/model");GameObject value=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);PrefabUtility.UnpackPrefabInstance(value,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);return value;}
        private static T Load<T>(string guid)where T:UnityEngine.Object{return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));}
        private static Material Clone(Material source,string folder,string name){Require(source!=null,"Original prefab material missing");var copy=new Material(source){name=name};AssetDatabase.CreateAsset(copy,AssetDatabase.GenerateUniqueAssetPath(folder+"/"+name+".mat"));return copy;}
        private static Material NewStandard(Shader shader,string folder,string name,Color color,float metallic,float gloss){var material=new Material(shader){name=name,color=color};material.SetFloat("_Metallic",metallic);material.SetFloat("_Glossiness",gloss);AssetDatabase.CreateAsset(material,AssetDatabase.GenerateUniqueAssetPath(folder+"/"+name+".mat"));return material;}
        private static GameObject Child(string name,Transform parent){var value=new GameObject(name);value.transform.SetParent(parent,false);return value;}
        private static string NewFolder(){if(!AssetDatabase.IsValidFolder(Runs))AssetDatabase.CreateFolder("Assets","GraceVWaterOriginalTestRuns");string path=AssetDatabase.GenerateUniqueAssetPath(Runs+"/Run_"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));AssetDatabase.CreateFolder(Runs,Path.GetFileName(path));return path;}
        private static Mesh Basin()
        {
            const int n=48;var vertices=new Vector3[(n+1)*(n+1)];var uv=new Vector2[vertices.Length];var triangles=new int[n*n*6];
            for(int z=0;z<=n;z++)for(int x=0;x<=n;x++){int i=z*(n+1)+x;float px=(x/(float)n-.5f)*8,pz=(z/(float)n-.5f)*8,r=Mathf.Sqrt(px*px+pz*pz);float y=-1.2f+1.35f*Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.2f,2.7f,r));vertices[i]=new Vector3(px,y,pz);uv[i]=new Vector2(px,pz);if(x==n||z==n)continue;int k=(z*n+x)*6;triangles[k]=i;triangles[k+1]=i+n+1;triangles[k+2]=i+1;triangles[k+3]=i+1;triangles[k+4]=i+n+1;triangles[k+5]=i+n+2;}
            var mesh=new Mesh{name="New opaque baseline basin",vertices=vertices,uv=uv,triangles=triangles};mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        private static void Require(bool okay,string message){if(!okay)throw new InvalidOperationException(message);}
    }
}
