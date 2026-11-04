"""Own Blender 4.3 script: data-only approved GLB -> Unity FBX, no rendering.
Run: blender --background --factory-startup --python this.py -- input.glb outdir
"""
import bpy
import hashlib
import json
import math
import pathlib
import struct
import sys
from mathutils import Vector

source, target = map(pathlib.Path, sys.argv[sys.argv.index("--") + 1:])
data = source.read_bytes()
expected = "73813c1a1581cd91407328d1e8a41dcdeb64ea319738288f81cfc3773d684de7"
assert hashlib.sha256(data).hexdigest() == expected, "Not approved original 50-WIDE source"
length, kind = struct.unpack_from("<II", data, 12)
assert kind == 0x4E4F534A
gltf = json.loads(data[20:20 + length])
bin_length, bin_kind = struct.unpack_from("<II", data, 20 + length)
assert bin_kind == 0x004E4942
binary = data[28 + length:28 + length + bin_length]
target.mkdir(parents=True, exist_ok=True)
image_name = "DroneUnderside.png"
for image in gltf.get("images", []):
    view = gltf["bufferViews"][image["bufferView"]]
    assert image["mimeType"] == "image/png"
    png = binary[view.get("byteOffset", 0):view.get("byteOffset", 0) + view["byteLength"]]
    (target / image_name).write_bytes(png)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.scene.unit_settings.system = "METRIC"
bpy.context.scene.unit_settings.scale_length = 1
bpy.ops.import_scene.gltf(filepath=str(source.resolve()), merge_vertices=False)
objects = list(bpy.context.scene.objects)
hubs = sorted((o for o in objects if o.name.startswith("Fan hub")), key=lambda o: o.matrix_world.translation.x)
blades = [o for o in objects if o.name.startswith("Low-poly fan blade")]
assert len(hubs) == 2 and len(blades) == 10, "Actual original fan rig missing"
for hub, x in zip(hubs, (-0.54, 0.54)):
    assert (hub.matrix_world.translation - Vector((x, -0.85, 0.96))).length < 1e-5, "Unexpected GLTF->Blender axis conversion"

def marker(name, position, parent=None):
    obj = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(obj)
    obj.parent = parent
    obj.matrix_world.translation = Vector(position)
    obj.empty_display_type = "PLAIN_AXES"
    obj.empty_display_size = .1
    return obj

# These six EMPTY nodes change no source mesh/canopy. They let Unity validate
# imported axis/unit conversion instead of guessing a GLB/FBX fan orientation.
marker("GraceRig_Origin", (0, 0, 0))
marker("GraceRig_X", (1, 0, 0))
marker("GraceRig_Y", (0, 0, 1))
marker("GraceRig_Z", (0, -1, 0))
for i, hub in enumerate(hubs):
    marker("GraceFanUp_" + str(i), hub.matrix_world.translation + Vector((0, 0, .25)), hub)

materials = []
for i, material in enumerate(gltf["materials"]):
    original_name = material["name"]
    actual = bpy.data.materials.get(original_name)
    assert actual is not None, original_name
    export_name = "GraceDroneMat_%02d" % i
    actual.name = export_name
    pbr = material.get("pbrMetallicRoughness", {})
    materials.append({"name": export_name, "sourceName": original_name,
        "color": pbr.get("baseColorFactor", [1, 1, 1, 1]),
        "metallic": pbr.get("metallicFactor", 1), "roughness": pbr.get("roughnessFactor", 1),
        "alphaBlend": material.get("alphaMode") == "BLEND", "doubleSided": material.get("doubleSided", False),
        "texture": image_name if "baseColorTexture" in pbr else ""})
(target / "DroneMaterials.json").write_text(json.dumps({"materials": materials}, indent=2) + "\n")

def snapshot():
    result = {}
    for obj in bpy.context.scene.objects:
        if obj.type != "MESH":
            continue
        obj.data.calc_loop_triangles()
        coords = [obj.matrix_world @ v.co for v in obj.data.vertices]
        result[obj.name] = {"triangles": len(obj.data.loop_triangles),
            "min": [min(p[a] for p in coords) for a in range(3)],
            "max": [max(p[a] for p in coords) for a in range(3)]}
    return result

before = snapshot()
output = target / "Drone50Wide.fbx"
assert not output.exists(), "Do not overwrite an existing prepared FBX"
bpy.ops.export_scene.fbx(filepath=str(output.resolve()), use_selection=False,
    object_types={"MESH", "EMPTY"}, global_scale=1, apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y",
    bake_space_transform=False, use_triangles=True, add_leaf_bones=False,
    bake_anim=False, path_mode="COPY", embed_textures=True)

# Reimport ONLY as data; this is not Unity import or rendered proof.
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(output.resolve()), use_custom_normals=True)
after = snapshot()
assert set(before) == set(after), "FBX lost or substituted source mesh objects"
worst = 0
for name, record in before.items():
    assert record["triangles"] == after[name]["triangles"], name
    for side in ("min", "max"):
        worst = max(worst, *(abs(a - b) for a, b in zip(record[side], after[name][side])))
assert worst < 1e-4, "FBX roundtrip geometry/unit error: " + str(worst)
for name in ("GraceRig_Origin", "GraceRig_X", "GraceRig_Y", "GraceRig_Z", "GraceFanUp_0", "GraceFanUp_1"):
    assert bpy.data.objects.get(name), "Axis marker lost: " + name
report = {"source": source.name, "source_sha256": expected, "fbx_sha256": hashlib.sha256(output.read_bytes()).hexdigest(),
    "blender": bpy.app.version_string, "mesh_objects": len(before), "triangles": sum(v["triangles"] for v in before.values()),
    "actual_fan_hubs": len(hubs), "actual_blades": len(blades), "extra_nonrendered_axis_markers": 6,
    "roundtrip_max_bounds_error_m": worst, "roundtrip_mesh_triangle_counts_unchanged": True,
    "unity_imported": False, "unity_compiled": False, "rendered": False,
    "note": "Approved geometry preserved by bounded numerical checks; Unity Standard material-factor conversion is not a rendered GLTF pixel-parity claim.", "meshes": before}
(target / "DroneProvenance.json").write_text(json.dumps(report, indent=2) + "\n")
print("PASS: approved GLB->FBX conversion, source geometry counts/bounds and markers; NOT Unity import/render validation.")
