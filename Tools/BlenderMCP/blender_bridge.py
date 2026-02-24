"""Blender HTTP bridge for ByteWar.

This script is meant to be executed *by Blender*:

  blender --background --factory-startup --python Tools/BlenderMCP/blender_bridge.py -- --port=8766

It exposes a small localhost-only HTTP API for safe, deterministic operations.
"""

from __future__ import annotations

import argparse
import json
import math
import os
import random
import sys
import traceback
from http.server import BaseHTTPRequestHandler, HTTPServer
from typing import Any, Dict


def _require_bpy():
    try:
        import bpy  # type: ignore

        return bpy
    except Exception as e:  # pragma: no cover
        print(
            "[BlenderBridge] ERROR: This script must be run inside Blender (bpy unavailable). "
            f"Exception: {e}",
            file=sys.stderr,
        )
        raise


def _json_response(
    handler: BaseHTTPRequestHandler, code: int, payload: Dict[str, Any]
) -> None:
    data = json.dumps(payload).encode("utf-8")
    handler.send_response(code)
    handler.send_header("Content-Type", "application/json")
    handler.send_header("Content-Length", str(len(data)))
    handler.end_headers()
    handler.wfile.write(data)


def _read_json(handler: BaseHTTPRequestHandler) -> Dict[str, Any]:
    length = int(handler.headers.get("Content-Length", "0"))
    raw = handler.rfile.read(length) if length > 0 else b"{}"
    try:
        return json.loads(raw.decode("utf-8"))
    except Exception:
        return {}


def cmd_health(bpy) -> Dict[str, Any]:
    return {
        "status": "ok",
        "blender": getattr(bpy.app, "version_string", "unknown"),
        "file": bpy.data.filepath,
        "workspaceRoot": os.environ.get("BYTEWAR_WORKSPACE_ROOT", ""),
    }


def cmd_reset_scene(bpy) -> Dict[str, Any]:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)

    # Also purge orphaned datablocks (best-effort; not all versions support all options)
    try:
        bpy.ops.outliner.orphans_purge(do_recursive=True)
    except Exception:
        pass

    return {"status": "ok"}


def cmd_list_objects(bpy) -> Dict[str, Any]:
    objs = []
    for o in bpy.data.objects:
        objs.append(
            {
                "name": o.name,
                "type": o.type,
                "parent": o.parent.name if o.parent else None,
            }
        )
    return {"status": "ok", "objects": objs}


def cmd_add_primitive(bpy, params: Dict[str, Any]) -> Dict[str, Any]:
    prim_type = str(params.get("type", "cube"))
    name = params.get("name")
    size = float(params.get("size", 1.0))
    location = params.get("location", [0.0, 0.0, 0.0])
    rotation = params.get("rotationEuler", [0.0, 0.0, 0.0])
    scale = params.get("scale", [1.0, 1.0, 1.0])

    bpy.ops.object.select_all(action="DESELECT")

    if prim_type == "cube":
        bpy.ops.mesh.primitive_cube_add(size=size, location=location, rotation=rotation)
    elif prim_type == "uv_sphere":
        bpy.ops.mesh.primitive_uv_sphere_add(
            radius=size / 2.0, location=location, rotation=rotation
        )
    elif prim_type == "ico_sphere":
        bpy.ops.mesh.primitive_ico_sphere_add(
            radius=size / 2.0, location=location, rotation=rotation
        )
    elif prim_type == "cylinder":
        bpy.ops.mesh.primitive_cylinder_add(
            radius=size / 2.0, depth=size, location=location, rotation=rotation
        )
    elif prim_type == "cone":
        bpy.ops.mesh.primitive_cone_add(
            radius1=size / 2.0, depth=size, location=location, rotation=rotation
        )
    elif prim_type == "plane":
        bpy.ops.mesh.primitive_plane_add(
            size=size, location=location, rotation=rotation
        )
    else:
        return {"status": "error", "error": f"Unsupported primitive type '{prim_type}'"}

    obj = bpy.context.active_object
    if obj is None:
        return {"status": "error", "error": "No active object after primitive add."}

    if name:
        obj.name = str(name)

    obj.scale = scale

    return {"status": "ok", "activeObject": obj.name}


def cmd_delete_object(bpy, params: Dict[str, Any]) -> Dict[str, Any]:
    target = str(params.get("name", ""))
    if not target:
        return {"status": "error", "error": "Missing 'name'"}

    obj = bpy.data.objects.get(target)
    if obj is None:
        return {"status": "error", "error": f"Object not found: {target}"}

    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.delete(use_global=False)
    return {"status": "ok"}


def cmd_export_fbx(bpy, params: Dict[str, Any]) -> Dict[str, Any]:
    file_path = str(params.get("filePath", ""))
    selected_only = bool(params.get("selectedOnly", False))

    if not file_path:
        return {"status": "error", "error": "Missing 'filePath'"}

    out_dir = os.path.dirname(file_path)
    if out_dir:
        os.makedirs(out_dir, exist_ok=True)

    # Ensure FBX exporter is available (usually built-in)
    try:
        bpy.ops.export_scene.fbx(
            filepath=file_path,
            use_selection=selected_only,
            # Unity-friendly axis mapping
            axis_forward="-Z",
            axis_up="Y",
            apply_unit_scale=True,
            bake_space_transform=True,
            add_leaf_bones=False,
            use_mesh_modifiers=True,
            mesh_smooth_type="FACE",
            path_mode="AUTO",
        )
    except Exception as e:
        return {"status": "error", "error": f"FBX export failed: {e}"}

    return {"status": "ok", "filePath": file_path}


def cmd_execute_script(bpy, params: Dict[str, Any]) -> Dict[str, Any]:
    python = params.get("python")
    if not isinstance(python, str) or not python.strip():
        return {"status": "error", "error": "Missing 'python'"}

    # Very small safety gate: block obvious network usage.
    blocked = ("import requests", "urllib", "socket", "http.client")
    if any(b in python for b in blocked):
        return {
            "status": "error",
            "error": "Network-related modules are not allowed in blender_execute_script.",
        }

    locals_dict: Dict[str, Any] = {"bpy": bpy}
    try:
        exec(python, {"__builtins__": __builtins__}, locals_dict)
    except Exception:
        return {"status": "error", "error": traceback.format_exc()}

    # If the script set a result, return it.
    result = locals_dict.get("result")
    return {"status": "ok", "result": result}


def _ensure_active_object(bpy, obj):
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)


def _apply_object_transforms(bpy, obj) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    _ensure_active_object(bpy, obj)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)


def _smart_uv_project(bpy, obj) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    _ensure_active_object(bpy, obj)

    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    try:
        bpy.ops.uv.smart_project(angle_limit=66.0, island_margin=0.02)
    finally:
        bpy.ops.object.mode_set(mode="OBJECT")


def _apply_decimate(bpy, obj, ratio: float) -> None:
    mod = obj.modifiers.new(name="Decimate", type="DECIMATE")
    mod.ratio = float(ratio)
    bpy.ops.object.select_all(action="DESELECT")
    _ensure_active_object(bpy, obj)
    bpy.ops.object.modifier_apply(modifier=mod.name)


def _count_tris(mesh) -> int:
    # loop_triangles requires calc; do best-effort.
    try:
        mesh.calc_loop_triangles()
        return len(mesh.loop_triangles)
    except Exception:
        try:
            return int(len(mesh.polygons) * 2)
        except Exception:
            return 0


def _ensure_preview_camera_and_light(bpy, scene):
    cam_name = "E2E_Camera"
    light_name = "E2E_KeyLight"

    cam_obj = bpy.data.objects.get(cam_name)
    if cam_obj is None:
        cam_data = bpy.data.cameras.new(cam_name)
        cam_obj = bpy.data.objects.new(cam_name, cam_data)
        scene.collection.objects.link(cam_obj)

    light_obj = bpy.data.objects.get(light_name)
    if light_obj is None:
        light_data = bpy.data.lights.new(light_name, type="SUN")
        light_data.energy = 3.0
        light_obj = bpy.data.objects.new(light_name, light_data)
        scene.collection.objects.link(light_obj)

    return cam_obj, light_obj


def _aim_camera_at(cam_obj, target) -> None:
    if target is None:
        cam_obj.rotation_euler = (math.radians(65.0), 0.0, math.radians(45.0))
        return

    try:
        import mathutils  # type: ignore

        direction = target - mathutils.Vector(cam_obj.location)
        cam_obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    except Exception:
        cam_obj.rotation_euler = (math.radians(65.0), 0.0, math.radians(45.0))


def _set_preview_render_settings(
    scene, file_path: str, width: int, height: int
) -> None:
    scene.render.engine = "CYCLES"
    try:
        scene.cycles.device = "CPU"
        scene.cycles.samples = 32
    except Exception:
        pass

    scene.render.resolution_x = width
    scene.render.resolution_y = height
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = file_path


def _set_preview_world_background(bpy, scene) -> None:
    try:
        if scene.world is None:
            scene.world = bpy.data.worlds.new("E2E_World")
        scene.world.use_nodes = True
        bg = scene.world.node_tree.nodes.get("Background")
        if bg is not None:
            bg.inputs[0].default_value = (0.18, 0.18, 0.18, 1.0)
            bg.inputs[1].default_value = 1.0
    except Exception:
        pass


def cmd_generate_e2e_prop(bpy, params: Dict[str, Any]) -> Dict[str, Any]:
    """Generate a deterministic environment prop with UVs, LOD0/1/2 and a collider mesh.

    Output object naming (Unity side convention):
      - <AssetName>_LOD0 / _LOD1 / _LOD2
      - <AssetName>_COL (collider mesh; should not render in Unity prefab)
    """

    asset_name = str(params.get("assetName") or "BlenderE2EProp")

    # Determinism: fixed seed for any noise/random used.
    random.seed(12345)

    cmd_reset_scene(bpy)

    # Base mesh: ico sphere + displacement for a "rock-ish" silhouette.
    bpy.ops.object.select_all(action="DESELECT")
    bpy.ops.mesh.primitive_ico_sphere_add(
        subdivisions=4, radius=0.55, location=(0.0, 0.0, 0.55)
    )
    base = bpy.context.active_object
    if base is None:
        return {"status": "error", "error": "Failed to create base mesh."}

    base.name = f"{asset_name}_LOD0"

    # Smooth shading for better preview; Unity can override.
    try:
        bpy.ops.object.shade_smooth()
    except Exception:
        pass

    try:
        base.data.use_auto_smooth = True
        base.data.auto_smooth_angle = math.radians(60.0)
    except Exception:
        pass

    # Displace modifier with Clouds texture.
    try:
        tex = bpy.data.textures.new(name="E2E_Clouds", type="CLOUDS")
        tex.noise_scale = 0.75
        displace = base.modifiers.new(name="Displace", type="DISPLACE")
        displace.texture = tex
        displace.strength = 0.18
        bpy.ops.object.select_all(action="DESELECT")
        _ensure_active_object(bpy, base)
        bpy.ops.object.modifier_apply(modifier=displace.name)
    except Exception:
        # If textures/modifiers are unavailable in this build, proceed with the base mesh.
        pass

    _smart_uv_project(bpy, base)
    _apply_object_transforms(bpy, base)

    # LOD1/LOD2 duplicates with decimation.
    bpy.ops.object.select_all(action="DESELECT")
    _ensure_active_object(bpy, base)
    bpy.ops.object.duplicate()
    lod1 = bpy.context.active_object
    if lod1 is None:
        return {"status": "error", "error": "Failed to duplicate for LOD1."}
    lod1.name = f"{asset_name}_LOD1"
    _apply_decimate(bpy, lod1, ratio=0.45)
    _apply_object_transforms(bpy, lod1)

    bpy.ops.object.select_all(action="DESELECT")
    _ensure_active_object(bpy, base)
    bpy.ops.object.duplicate()
    lod2 = bpy.context.active_object
    if lod2 is None:
        return {"status": "error", "error": "Failed to duplicate for LOD2."}
    lod2.name = f"{asset_name}_LOD2"
    _apply_decimate(bpy, lod2, ratio=0.20)
    _apply_object_transforms(bpy, lod2)

    # Collider mesh: very low-poly version; doesn't need UVs.
    bpy.ops.object.select_all(action="DESELECT")
    _ensure_active_object(bpy, lod2)
    bpy.ops.object.duplicate()
    col = bpy.context.active_object
    if col is None:
        return {"status": "error", "error": "Failed to duplicate for collider mesh."}
    col.name = f"{asset_name}_COL"
    _apply_decimate(bpy, col, ratio=0.10)
    _apply_object_transforms(bpy, col)

    # Select only our export objects (so export_selectedOnly works from Node).
    bpy.ops.object.select_all(action="DESELECT")
    for o in (base, lod1, lod2, col):
        o.select_set(True)
    bpy.context.view_layer.objects.active = base

    def summarize(obj):
        mesh = getattr(obj, "data", None)
        tris = _count_tris(mesh) if mesh is not None else 0
        return {"name": obj.name, "type": obj.type, "triangles": tris}

    return {
        "status": "ok",
        "assetName": asset_name,
        "objects": [summarize(base), summarize(lod1), summarize(lod2), summarize(col)],
    }


def cmd_render_preview(bpy, params: Dict[str, Any]) -> Dict[str, Any]:
    asset_name = str(params.get("assetName") or "BlenderE2EProp")
    file_path = str(params.get("filePath") or "")
    width = int(params.get("width") or 512)
    height = int(params.get("height") or 512)

    if not file_path:
        return {"status": "error", "error": "Missing 'filePath'"}

    out_dir = os.path.dirname(file_path)
    if out_dir:
        os.makedirs(out_dir, exist_ok=True)

    # Ensure prop exists; if not, generate it.
    if bpy.data.objects.get(f"{asset_name}_LOD0") is None:
        gen = cmd_generate_e2e_prop(bpy, {"assetName": asset_name})
        if gen.get("status") != "ok":
            return gen

    scene = bpy.context.scene

    cam_obj, light_obj = _ensure_preview_camera_and_light(bpy, scene)
    cam_obj.location = (2.7, -2.7, 1.8)
    light_obj.location = (4.0, -4.0, 6.0)

    try:
        import mathutils  # type: ignore

        _aim_camera_at(cam_obj, mathutils.Vector((0.0, 0.0, 0.6)))
    except Exception:
        _aim_camera_at(cam_obj, None)

    scene.camera = cam_obj

    _set_preview_render_settings(scene, file_path=file_path, width=width, height=height)
    _set_preview_world_background(bpy, scene)

    try:
        bpy.ops.render.render(write_still=True)
    except Exception as e:
        return {"status": "error", "error": f"Render failed: {e}"}

    return {"status": "ok", "filePath": file_path, "width": width, "height": height}


def dispatch(bpy, command: str, params: Dict[str, Any]) -> Dict[str, Any]:
    if command == "health":
        return cmd_health(bpy)
    if command == "reset_scene":
        return cmd_reset_scene(bpy)
    if command == "list_objects":
        return cmd_list_objects(bpy)
    if command == "add_primitive":
        return cmd_add_primitive(bpy, params)
    if command == "delete_object":
        return cmd_delete_object(bpy, params)
    if command == "export_fbx":
        return cmd_export_fbx(bpy, params)
    if command == "generate_e2e_prop":
        return cmd_generate_e2e_prop(bpy, params)
    if command == "render_preview":
        return cmd_render_preview(bpy, params)
    if command == "execute_script":
        return cmd_execute_script(bpy, params)

    return {"status": "error", "error": f"Unknown command: {command}"}


class Handler(BaseHTTPRequestHandler):
    def log_message(self, format: str, *args: Any) -> None:
        # Reduce noise; the parent Node process captures stdout/stderr.
        return

    def do_GET(self):
        bpy = _require_bpy()
        if self.path == "/health":
            _json_response(self, 200, cmd_health(bpy))
            return
        _json_response(self, 404, {"status": "error", "error": "Not found"})

    def do_POST(self):
        bpy = _require_bpy()
        if self.path != "/execute":
            _json_response(self, 404, {"status": "error", "error": "Not found"})
            return

        body = _read_json(self)
        command = str(body.get("command", ""))
        params = body.get("params")
        if not isinstance(params, dict):
            params = {}

        if not command:
            _json_response(self, 400, {"status": "error", "error": "Missing 'command'"})
            return

        try:
            result = dispatch(bpy, command, params)
            if result.get("status") == "error":
                _json_response(self, 400, result)
                return
            _json_response(self, 200, result)
        except Exception:
            _json_response(
                self, 500, {"status": "error", "error": traceback.format_exc()}
            )


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(add_help=False)
    parser.add_argument("--port", type=int, default=8766)
    args, _unknown = parser.parse_known_args(argv)

    server = HTTPServer(("127.0.0.1", args.port), Handler)
    print(f"[BlenderBridge] Listening on http://127.0.0.1:{args.port}/")
    server.serve_forever()
    return 0


if __name__ == "__main__":
    # Blender passes args after '--' to Python script.
    # When invoked from Node, we always include '-- --port=####'.
    port_args = []
    if "--" in sys.argv:
        port_args = sys.argv[sys.argv.index("--") + 1 :]
    else:
        port_args = sys.argv[1:]
    raise SystemExit(main(port_args))
