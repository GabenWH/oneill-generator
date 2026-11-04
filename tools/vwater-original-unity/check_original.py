#!/usr/bin/env python3
"""Own portable source/asset checks. Never runs Unity or vendor scripts."""
import hashlib,json,pathlib,re
root=pathlib.Path(__file__).resolve().parents[2]
def require(v,m):
    if not v:raise AssertionError(m)
def read(p):return (root/p).read_text(encoding='utf-8-sig')
manifest=json.loads(read('Assets/GraceVWaterOriginalVendor/UPSTREAM-PROVENANCE.json'))
require(manifest['commit']=='fb156e302f213a507956de0db1a442c17e22d6b5','Pinned original version')
for f in manifest['files']:
    data=(root/f['vendor_path']).read_bytes()
    require(len(data)==f['bytes'] and hashlib.sha256(data).hexdigest()==f['sha256'],f['source_path'])
    require(hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest()==f['git_blob'],'Original Git blob: '+f['source_path'])
require(len(manifest['files'])==150,'Complete selected pinned original records')
guids={}
for p in (root/'Assets').rglob('*.meta'):
    match=re.search(r'^guid: (\w+)',p.read_text(encoding='utf-8-sig'),re.M)
    if not match:continue
    guid=match.group(1);require(guid not in guids,'Duplicate GUID: '+guid);guids[guid]=p.with_suffix('')
for top in ['Assets/GraceVWaterOriginalVendor','Assets/GraceVWaterOriginalTest']:
    for p in [root/top]+list((root/top).rglob('*')):
        if p.suffix!='.meta':require(pathlib.Path(str(p)+'.meta').is_file(),'Missing asset/folder metadata: '+str(p))
required=json.loads(read('Assets/GraceVWaterOriginalTest/Editor/OriginalRequiredAssets.json'))['assets']
require(len(required)==29,'Original rig-side preflight coverage')
for f in required:require(f['guid'] in guids and hashlib.sha256(guids[f['guid']].read_bytes()).hexdigest()==f['sha256'],'Rig-side hash record: '+f['sourcePath'])
must={'Assets/VWater/Prefabs/VWater.prefab','Assets/VWater/Prefabs/WaterManager.prefab','Assets/VWater/Materials/VWater Default.mat','Assets/VWater/Materials/VWater Bottom.mat','Assets/VWater/Materials/VWater_View.mat'}
require(must<={f['sourcePath'] for f in required},'Original prefab/material guard regression')
for name,guid in [('water_NRM_smooth.png','db8139fc50a0c48fd949ee7594cc977c'),('foam.png','2c3a5c50e55575b43a806489841e69ff'),('flowNoise.tga','c9f3a590d0e6c45629d6d810b88918a2')]:require(guids[guid].name==name,'Original material texture dependency')
builder=read('Assets/GraceVWaterOriginalTest/Editor/GraceOriginalVWaterSceneBuilder.cs');bridge=read('Assets/GraceVWaterOriginalTest/Runtime/GraceOriginalVWaterFanBridge.cs')
require('.normalized.Dot(' not in builder,'Unity Vector3.Dot static API regression')
require('slots[i]!=null&&materials.TryGetValue' not in builder,'C# out-variable definite-assignment regression')
require(builder.index('stage.SetActive(false)')<builder.index('Clone(meshRenderer.sharedMaterials')<builder.index('stage.SetActive(true)'),'Original shared material activation safety')
require('SaveCurrentModifiedScenesIfUserWantsTo' in builder and 'GenerateUniqueAssetPath' in builder,'Save/cancel and unique-new output')
require('InitializeOnLoad' not in builder and 'OnPostprocess' not in builder,'No automatic import/startup code')
require('private void LateUpdate()' in bridge and 'originalDynamics.AddFlow(' in bridge,'Original API bridge timing')
require('Dispatch(' not in bridge and 'ComputeBuffer' not in bridge,'No replacement ripple backend')
provenance=json.loads(read('Assets/GraceVWaterOriginalTest/PreparedDrone/DroneProvenance.json'))
fbx=root/'Assets/GraceVWaterOriginalTest/PreparedDrone/Drone50Wide.fbx'
require(hashlib.sha256(fbx.read_bytes()).hexdigest()==provenance['fbx_sha256'],'Prepared actual FBX changed')
require(provenance['source_sha256']=='73813c1a1581cd91407328d1e8a41dcdeb64ea319738288f81cfc3773d684de7','Approved exact 50-WIDE source')
require(provenance['mesh_objects']==36 and provenance['triangles']==3514 and provenance['actual_fan_hubs']==2 and provenance['actual_blades']==10,'Actual geometry/rig provenance')
require(provenance['roundtrip_max_bounds_error_m']<1e-4 and not provenance['unity_imported'] and not provenance['rendered'],'Bounded FBX evidence, not Unity proof')
require('530 own fan-input math assertions' in read('docs/vwater-original-unity/MATH-TESTS.txt'),'Actual own math test log')
require('4 authored Unity C# files parse as C#9' in read('docs/vwater-original-unity/SYNTAX-ONLY.txt'),'Actual syntax-only test log')
print('PASS:150 exact original records,29 rig preflight assets, original texture/GUID closure, safe source wiring, approved FBX and own test evidence. Unity compile/Play/GPU UNRUN.')
