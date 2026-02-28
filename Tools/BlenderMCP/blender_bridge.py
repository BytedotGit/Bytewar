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
from typing import Any, Dict, List, Tuple


MISSING_FILE_PATH_ERROR = "Missing 'filePath'"


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
        return {"status": "error", "error": MISSING_FILE_PATH_ERROR}

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


def _build_tree_material(bpy, name: str, rgba, roughness: float, metallic: float = 0.0):
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name=name)

    mat.use_nodes = True
    nt = mat.node_tree
    nodes = nt.nodes
    links = nt.links
    nodes.clear()

    out = nodes.new(type="ShaderNodeOutputMaterial")
    out.location = (280, 0)
    bsdf = nodes.new(type="ShaderNodeBsdfPrincipled")
    bsdf.location = (0, 0)
    bsdf.inputs["Base Color"].default_value = rgba
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return mat


def _assign_single_material(obj: Any, mat: Any):
    if obj is None or getattr(obj, "data", None) is None:
        return
    mats = obj.data.materials
    mats.clear()
    mats.append(mat)


def _add_large_tree_trunk_segment(
    bpy,
    *,
    name: str,
    radius_base: float,
    radius_top: float,
    depth: float,
    location: Tuple[float, float, float],
    rotation_deg: Tuple[float, float, float],
    displace_strength: float = 0.07,
    displace_scale: float = 1.45,
) -> Any:
    bpy.ops.object.select_all(action="DESELECT")
    bpy.ops.mesh.primitive_cone_add(
        vertices=6,
        radius1=radius_base,
        radius2=radius_top,
        depth=depth,
        location=location,
        rotation=(
            math.radians(rotation_deg[0]),
            math.radians(rotation_deg[1]),
            math.radians(rotation_deg[2]),
        ),
    )
    obj = bpy.context.active_object

    if obj is None:
        return None

    obj.name = name

    try:
        tex = bpy.data.textures.new(name=f"{name}_Noise", type="CLOUDS")
        tex.noise_scale = displace_scale
        displace = obj.modifiers.new(name=f"{name}_Displace", type="DISPLACE")
        displace.texture = tex
        displace.strength = displace_strength
        _ensure_active_object(bpy, obj)
        bpy.ops.object.modifier_apply(modifier=displace.name)
    except Exception:
        pass

    return obj


def _add_geometric_trunk_segment(
    bpy,
    *,
    name: str,
    radius_base: float,
    radius_top: float,
    depth: float,
    location: Tuple[float, float, float],
    vertices: int,
    rotation_deg: Tuple[float, float, float] = (0.0, 0.0, 0.0),
) -> Any:
    bpy.ops.object.select_all(action="DESELECT")
    bpy.ops.mesh.primitive_cone_add(
        vertices=max(3, int(vertices)),
        radius1=radius_base,
        radius2=radius_top,
        depth=depth,
        location=location,
        rotation=(
            math.radians(rotation_deg[0]),
            math.radians(rotation_deg[1]),
            math.radians(rotation_deg[2]),
        ),
    )
    obj = bpy.context.active_object
    if obj is None:
        return None

    obj.name = name
    return obj


def _add_geometric_segment_between_points(
    bpy,
    *,
    name: str,
    base_point: Tuple[float, float, float],
    tip_point: Tuple[float, float, float],
    radius_base: float,
    radius_top: float,
    vertices: int,
) -> Any:
    dx = tip_point[0] - base_point[0]
    dy = tip_point[1] - base_point[1]
    dz = tip_point[2] - base_point[2]
    depth = math.sqrt(dx * dx + dy * dy + dz * dz)
    if depth <= 1e-4:
        return None

    location = (
        (base_point[0] + tip_point[0]) * 0.5,
        (base_point[1] + tip_point[1]) * 0.5,
        (base_point[2] + tip_point[2]) * 0.5,
    )

    rotation_deg = (0.0, 0.0, 0.0)
    try:
        import mathutils  # type: ignore

        direction = mathutils.Vector((dx, dy, dz)).normalized()
        quat = direction.to_track_quat("Z", "Y")
        euler = quat.to_euler()
        rotation_deg = (
            math.degrees(euler.x),
            math.degrees(euler.y),
            math.degrees(euler.z),
        )
    except Exception:
        yaw = math.degrees(math.atan2(dy, dx))
        pitch = math.degrees(math.atan2(math.sqrt(dx * dx + dy * dy), max(1e-4, dz)))
        rotation_deg = (pitch, 0.0, yaw)

    return _add_geometric_trunk_segment(
        bpy,
        name=name,
        radius_base=max(0.01, float(radius_base)),
        radius_top=max(0.005, float(radius_top)),
        depth=depth,
        location=location,
        vertices=vertices,
        rotation_deg=rotation_deg,
    )


def _lerp(a: float, b: float, t: float) -> float:
    return a + (b - a) * t


def _lerp_point(
    a: Tuple[float, float, float], b: Tuple[float, float, float], t: float
) -> Tuple[float, float, float]:
    return (
        _lerp(float(a[0]), float(b[0]), t),
        _lerp(float(a[1]), float(b[1]), t),
        _lerp(float(a[2]), float(b[2]), t),
    )


def _normalize_vector(v: Tuple[float, float, float]) -> Tuple[float, float, float]:
    length = math.sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2])
    if length <= 1e-6:
        return (0.0, 0.0, 1.0)
    return (v[0] / length, v[1] / length, v[2] / length)


def _sample_polyline_point(
    points: List[Tuple[float, float, float]], t: float
) -> Tuple[float, float, float]:
    if len(points) == 0:
        return (0.0, 0.0, 0.0)
    if len(points) == 1:
        return points[0]

    clamped = max(0.0, min(1.0, float(t)))
    scaled = clamped * float(len(points) - 1)
    idx = int(math.floor(scaled))
    if idx >= len(points) - 1:
        return points[-1]

    local_t = scaled - float(idx)
    return _lerp_point(points[idx], points[idx + 1], local_t)


def _build_tapered_segment_chain(
    bpy,
    *,
    name_prefix: str,
    points: List[Tuple[float, float, float]],
    radius_base: float,
    radius_top: float,
    vertices: int,
) -> List[Any]:
    if len(points) < 2:
        return []

    segments: List[Any] = []
    total = len(points) - 1
    for idx in range(total):
        t0 = float(idx) / float(total)
        t1 = float(idx + 1) / float(total)
        seg = _add_geometric_segment_between_points(
            bpy,
            name=f"{name_prefix}_{idx}",
            base_point=points[idx],
            tip_point=points[idx + 1],
            radius_base=_lerp(radius_base, radius_top, t0),
            radius_top=_lerp(radius_base, radius_top, t1),
            vertices=vertices,
        )
        if seg is not None:
            segments.append(seg)

    return segments


def _build_curved_trunk_points(
    *,
    base: Tuple[float, float, float],
    top: Tuple[float, float, float],
    segment_count: int,
    sway: float,
    sway_phase: float,
    s_curve_bias: float = 0.0,
) -> List[Tuple[float, float, float]]:
    points: List[Tuple[float, float, float]] = []
    segment_total = max(2, int(segment_count))
    wind_dir = (math.cos(sway_phase + 0.9), math.sin(sway_phase + 0.9))
    for idx in range(segment_total + 1):
        t = float(idx) / float(segment_total)
        wave = math.sin((t * math.pi * 1.25) + sway_phase)
        side = math.cos((t * math.pi * 0.95) + (sway_phase * 0.7))
        attenuation = math.sin(t * math.pi)
        s_curve = attenuation * ((t - 0.5) * 2.0)
        points.append(
            (
                _lerp(base[0], top[0], t)
                + wave * sway * attenuation
                + wind_dir[0] * sway * s_curve_bias * s_curve,
                _lerp(base[1], top[1], t)
                + side * sway * 0.78 * attenuation
                + wind_dir[1] * sway * s_curve_bias * s_curve,
                _lerp(base[2], top[2], t),
            )
        )
    return points


def _resolve_broadleaf_shape_settings(
    profile: Dict[str, Any],
    trunk_top: Tuple[float, float, float],
) -> Dict[str, Any]:
    triad_canopy = bool(profile.get("triad_canopy", False))
    branch_levels = list(
        profile.get(
            "branch_levels",
            [0.62, 0.78] if triad_canopy else [0.53, 0.63, 0.72, 0.8, 0.87, 0.92],
        )
    )
    branch_yaws = list(
        profile.get(
            "branch_yaws",
            [228.0, 24.0] if triad_canopy else [12.0, 72.0, 132.0, 192.0, 252.0, 312.0],
        )
    )
    branch_lengths = list(
        profile.get(
            "branch_lengths",
            [1.52, 1.46] if triad_canopy else [1.95, 1.82, 1.72, 1.62, 1.52, 1.42],
        )
    )
    canopy_radius_base = float(
        profile.get("canopy_radius", 1.28 if triad_canopy else 1.08)
    )
    canopy_z_scale = float(
        profile.get("canopy_z_scale", 0.62 if triad_canopy else 0.66)
    )
    main_canopy_offset = tuple(
        profile.get("main_canopy_offset", (0.02, 0.02, 0.46 if triad_canopy else 0.52))
    )
    main_canopy_center = (
        trunk_top[0] + float(main_canopy_offset[0]),
        trunk_top[1] + float(main_canopy_offset[1]),
        trunk_top[2] + float(main_canopy_offset[2]),
    )
    main_canopy_radius = float(
        profile.get(
            "main_canopy_radius", canopy_radius_base * (1.28 if triad_canopy else 0.9)
        )
    )

    return {
        "triad_canopy": triad_canopy,
        "branch_levels": branch_levels,
        "branch_yaws": branch_yaws,
        "branch_lengths": branch_lengths,
        "branch_rise": float(profile.get("branch_rise", 0.1 if triad_canopy else 0.14)),
        "canopy_radius_base": canopy_radius_base,
        "canopy_z_scale": canopy_z_scale,
        "main_canopy_center": main_canopy_center,
        "main_canopy_radius": main_canopy_radius,
        "canopy_merge_pull": float(
            profile.get("canopy_merge_pull", 0.36 if triad_canopy else 0.0)
        ),
        "side_canopy_scales": list(profile.get("side_canopy_scales", [1.0, 0.94])),
        # Keep branch endpoints visually connected to side lobes after fusion/decimation.
        "tip_connector_radius_scale": float(
            profile.get("tip_connector_radius_scale", 0.68 if triad_canopy else 0.42)
        ),
        "tip_connector_forward_offset": float(
            profile.get("tip_connector_forward_offset", 0.02 if triad_canopy else 0.03)
        ),
        "tip_connector_z_offset": float(
            profile.get("tip_connector_z_offset", 0.0 if triad_canopy else 0.02)
        ),
        "canopy_bridge_t": float(
            profile.get("canopy_bridge_t", 0.36 if triad_canopy else 0.3)
        ),
        "canopy_bridge_radius_scale": float(
            profile.get("canopy_bridge_radius_scale", 0.62 if triad_canopy else 0.38)
        ),
    }


def _compute_broadleaf_branch_points(
    *,
    anchor: Tuple[float, float, float],
    yaw_deg: float,
    length: float,
    branch_rise: float,
    triad_canopy: bool,
) -> Dict[str, Any]:
    yaw = math.radians(float(yaw_deg))
    direction = _normalize_vector((math.cos(yaw), math.sin(yaw), branch_rise))
    side = _normalize_vector((-direction[1], direction[0], 0.0))
    vertical = (
        length * branch_rise
        if triad_canopy
        else length * branch_rise + random.uniform(-0.1, 0.02)
    )

    tip = (
        anchor[0] + length * direction[0],
        anchor[1] + length * direction[1],
        anchor[2] + vertical,
    )

    if triad_canopy:
        bend_sign = 1.0 if math.sin((yaw * 1.7) + 0.32) >= 0.0 else -1.0
        bend_primary = length * 0.12
        bend_secondary = length * 0.06
        mid_a = (
            anchor[0]
            + length * 0.32 * direction[0]
            + side[0] * bend_primary * bend_sign,
            anchor[1]
            + length * 0.32 * direction[1]
            + side[1] * bend_primary * bend_sign,
            anchor[2] + vertical * 0.38 + 0.05,
        )
        mid_b = (
            anchor[0]
            + length * 0.66 * direction[0]
            - side[0] * bend_secondary * bend_sign,
            anchor[1]
            + length * 0.66 * direction[1]
            - side[1] * bend_secondary * bend_sign,
            anchor[2] + vertical * 0.76 + 0.08,
        )
        tip = (
            tip[0] + side[0] * length * 0.03 * bend_sign,
            tip[1] + side[1] * length * 0.03 * bend_sign,
            tip[2],
        )
        return {
            "points": [anchor, mid_a, mid_b, tip],
            "tip": tip,
            "forward_source": mid_b,
        }

    mid = (
        anchor[0] + length * 0.56 * direction[0],
        anchor[1] + length * 0.56 * direction[1],
        anchor[2] + vertical * 0.5 + random.uniform(-0.02, 0.04),
    )
    return {
        "points": [anchor, mid, tip],
        "tip": tip,
        "forward_source": mid,
    }


def _build_broadleaf_branch_parts(
    bpy,
    *,
    asset_name: str,
    branch_index: int,
    anchor: Tuple[float, float, float],
    branch_points: List[Tuple[float, float, float]],
    branch_radius: float,
    triad_canopy: bool,
    vertices: int,
) -> List[Any]:
    if len(branch_points) < 2:
        return []

    first_branch_point = branch_points[1]
    nub_dir = _normalize_vector(
        (
            first_branch_point[0] - anchor[0],
            first_branch_point[1] - anchor[1],
            first_branch_point[2] - anchor[2],
        )
    )

    # Start branch geometry slightly inside the trunk to avoid a detached look
    # once all wood parts are fused and smoothed.
    root_embed = 0.28 if triad_canopy else 0.22
    embedded_root = (
        anchor[0] - nub_dir[0] * root_embed,
        anchor[1] - nub_dir[1] * root_embed,
        anchor[2] - nub_dir[2] * root_embed,
    )
    blended_branch_points = [embedded_root] + list(branch_points)
    branch_parts = _build_tapered_segment_chain(
        bpy,
        name_prefix=f"{asset_name}_Branch_{branch_index}",
        points=blended_branch_points,
        radius_base=max(0.042, branch_radius * (1.42 if triad_canopy else 1.36)),
        radius_top=max(0.02, branch_radius * 0.42),
        vertices=max(5, vertices - 1),
    )

    nub_length = 0.34 if triad_canopy else 0.26
    nub_tip = (
        anchor[0] + nub_dir[0] * nub_length,
        anchor[1] + nub_dir[1] * nub_length,
        anchor[2] + nub_dir[2] * nub_length,
    )
    nub = _add_geometric_segment_between_points(
        bpy,
        name=f"{asset_name}_BranchJunction_{branch_index}",
        base_point=anchor,
        tip_point=nub_tip,
        radius_base=max(0.036, branch_radius * (1.16 if triad_canopy else 1.12)),
        radius_top=max(0.026, branch_radius * (0.84 if triad_canopy else 0.88)),
        vertices=vertices,
    )
    if nub is not None:
        branch_parts.append(nub)

    # Straddle the branch root with a short, wider segment so voxel fusion can
    # produce a continuous fillet where the branch meets the curved trunk.
    fillet_inset = 0.24 if triad_canopy else 0.2
    fillet_outset = 0.18 if triad_canopy else 0.15
    fillet_base = (
        anchor[0] - nub_dir[0] * fillet_inset,
        anchor[1] - nub_dir[1] * fillet_inset,
        anchor[2] - nub_dir[2] * fillet_inset,
    )
    fillet_tip = (
        anchor[0] + nub_dir[0] * fillet_outset,
        anchor[1] + nub_dir[1] * fillet_outset,
        anchor[2] + nub_dir[2] * fillet_outset,
    )
    fillet = _add_geometric_segment_between_points(
        bpy,
        name=f"{asset_name}_BranchFillet_{branch_index}",
        base_point=fillet_base,
        tip_point=fillet_tip,
        radius_base=max(0.046, branch_radius * (1.56 if triad_canopy else 1.46)),
        radius_top=max(0.034, branch_radius * (1.08 if triad_canopy else 1.02)),
        vertices=vertices,
    )
    if fillet is not None:
        branch_parts.append(fillet)

    return branch_parts


def _append_named_canopy(canopy_parts: List[Any], canopy_obj: Any, name: str) -> None:
    if canopy_obj is None:
        return
    canopy_obj.name = name
    canopy_parts.append(canopy_obj)


def _build_broadleaf_branch_canopy_parts(
    bpy,
    *,
    asset_name: str,
    branch_index: int,
    tip: Tuple[float, float, float],
    forward_source: Tuple[float, float, float],
    triad_canopy: bool,
    canopy_radius_base: float,
    canopy_z_scale: float,
    main_canopy_center: Tuple[float, float, float],
    canopy_merge_pull: float,
    side_canopy_scales: List[float],
    tip_connector_radius_scale: float,
    tip_connector_forward_offset: float,
    tip_connector_z_offset: float,
    canopy_bridge_t: float,
    canopy_bridge_radius_scale: float,
) -> List[Any]:
    canopy_parts: List[Any] = []
    tip_forward = _normalize_vector(
        (
            tip[0] - forward_source[0],
            tip[1] - forward_source[1],
            tip[2] - forward_source[2],
        )
    )
    raw_lobe_center = (
        tip[0] + tip_forward[0] * (0.22 if triad_canopy else 0.3),
        tip[1] + tip_forward[1] * (0.22 if triad_canopy else 0.3),
        tip[2] + (0.04 if triad_canopy else 0.18),
    )

    if triad_canopy:
        lobe_center = _lerp_point(
            raw_lobe_center, main_canopy_center, canopy_merge_pull
        )
        z_bias = -0.14 if branch_index == 0 else 0.12
        lobe_center = (
            lobe_center[0],
            lobe_center[1],
            lobe_center[2] + z_bias,
        )
        lobe_radius = canopy_radius_base * float(
            side_canopy_scales[branch_index % len(side_canopy_scales)]
        )
        side_scale = float(side_canopy_scales[branch_index % len(side_canopy_scales)])
        lobe = _add_large_tree_canopy_poly(
            bpy,
            loc=lobe_center,
            radius=lobe_radius,
            scale_xyz=(
                1.12 + side_scale * 0.22,
                0.88 + side_scale * 0.18,
                canopy_z_scale * (0.82 + side_scale * 0.11),
            ),
            rotation_deg=(0.0, 0.0, float(-18.0 if branch_index == 0 else 18.0)),
            subdivisions=0,
        )
        _append_named_canopy(
            canopy_parts,
            lobe,
            f"{asset_name}_Canopy_Side_{branch_index}",
        )

        # Add a larger tip connector and an intermediate bridge lobe so branch
        # endpoints visibly merge into the side canopy instead of appearing detached.
        connector_center = (
            tip[0] + tip_forward[0] * tip_connector_forward_offset,
            tip[1] + tip_forward[1] * tip_connector_forward_offset,
            tip[2] + tip_connector_z_offset,
        )
        connector = _add_large_tree_canopy_poly(
            bpy,
            loc=connector_center,
            radius=lobe_radius * tip_connector_radius_scale,
            scale_xyz=(
                1.06 + side_scale * 0.14,
                0.92 + side_scale * 0.12,
                canopy_z_scale * (0.74 + side_scale * 0.14),
            ),
            rotation_deg=(0.0, 0.0, float(-18.0 if branch_index == 0 else 18.0)),
            subdivisions=0,
        )
        _append_named_canopy(
            canopy_parts,
            connector,
            f"{asset_name}_Canopy_Side_{branch_index}_Root",
        )

        bridge_t = max(0.18, min(0.65, canopy_bridge_t))
        bridge_center = _lerp_point(connector_center, lobe_center, bridge_t)
        bridge = _add_large_tree_canopy_poly(
            bpy,
            loc=bridge_center,
            radius=lobe_radius * canopy_bridge_radius_scale,
            scale_xyz=(
                1.08 + side_scale * 0.12,
                0.92 + side_scale * 0.1,
                canopy_z_scale * (0.72 + side_scale * 0.12),
            ),
            rotation_deg=(0.0, 0.0, float(-18.0 if branch_index == 0 else 18.0)),
            subdivisions=0,
        )
        _append_named_canopy(
            canopy_parts,
            bridge,
            f"{asset_name}_Canopy_Side_{branch_index}_Bridge",
        )
        return canopy_parts

    lobe_radius = canopy_radius_base * random.uniform(0.9, 1.08)
    lobe = _add_large_tree_canopy_poly(
        bpy,
        loc=raw_lobe_center,
        radius=lobe_radius,
        scale_xyz=(
            random.uniform(0.95, 1.18),
            random.uniform(0.95, 1.18),
            canopy_z_scale * random.uniform(0.82, 1.0),
        ),
        rotation_deg=(
            random.uniform(-8.0, 8.0),
            random.uniform(-8.0, 8.0),
            random.uniform(-20.0, 20.0),
        ),
        subdivisions=1,
    )
    _append_named_canopy(canopy_parts, lobe, f"{asset_name}_Canopy_{branch_index}")

    side = _normalize_vector((-tip_forward[1], tip_forward[0], 0.0))
    side_lobe = _add_large_tree_canopy_poly(
        bpy,
        loc=(
            tip[0] + side[0] * 0.38,
            tip[1] + side[1] * 0.38,
            tip[2] + 0.06,
        ),
        radius=lobe_radius * 0.82,
        scale_xyz=(1.06, 1.06, canopy_z_scale * 0.72),
        rotation_deg=(
            random.uniform(-6.0, 6.0),
            random.uniform(-6.0, 6.0),
            random.uniform(-20.0, 20.0),
        ),
        subdivisions=1,
    )
    _append_named_canopy(
        canopy_parts,
        side_lobe,
        f"{asset_name}_Canopy_{branch_index}_Side",
    )

    return canopy_parts


def _build_geometric_broadleaf_tree(
    bpy,
    *,
    asset_name: str,
    spec: Dict[str, Any],
) -> Dict[str, Any]:
    trunk_spec = dict(spec.get("trunk", {}))
    profile = dict(spec.get("broadleaf_profile", {}))

    radius_base = float(trunk_spec.get("radius_base", 0.34))
    radius_top = float(trunk_spec.get("radius_top", 0.24))
    depth = float(trunk_spec.get("depth", 6.2))
    center = tuple(trunk_spec.get("location", (0.0, 0.0, 3.1)))
    vertices = max(5, int(trunk_spec.get("vertices", 7)))

    trunk_base = (
        float(center[0]),
        float(center[1]),
        float(center[2]) - depth * 0.5,
    )
    lean = tuple(profile.get("lean", (0.14, -0.09)))
    trunk_top = (
        float(center[0]) + float(lean[0]),
        float(center[1]) + float(lean[1]),
        float(center[2]) + depth * 0.5,
    )

    sway = float(profile.get("sway", 0.22))
    trunk_segments = _build_curved_trunk_points(
        base=trunk_base,
        top=trunk_top,
        segment_count=int(profile.get("trunk_segments", 5)),
        sway=sway,
        sway_phase=float(profile.get("sway_phase", random.uniform(-0.8, 0.8))),
        s_curve_bias=float(profile.get("s_curve_bias", 0.0)),
    )
    wood_parts = _build_tapered_segment_chain(
        bpy,
        name_prefix=f"{asset_name}_Trunk",
        points=trunk_segments,
        radius_base=radius_base * float(profile.get("base_flare", 1.16)),
        radius_top=radius_top,
        vertices=vertices,
    )

    shape_settings = _resolve_broadleaf_shape_settings(profile, trunk_top)
    canopy_parts: List[Any] = []

    for idx, level in enumerate(shape_settings["branch_levels"]):
        anchor = _sample_polyline_point(trunk_segments, float(level))
        yaws = shape_settings["branch_yaws"]
        base_yaw = float(yaws[idx % len(yaws)])
        yaw_deg = (
            base_yaw
            if bool(shape_settings["triad_canopy"])
            else base_yaw + random.uniform(-7.0, 7.0)
        )
        lengths = shape_settings["branch_lengths"]
        length = float(lengths[idx % len(lengths)])
        branch_data = _compute_broadleaf_branch_points(
            anchor=anchor,
            yaw_deg=yaw_deg,
            length=length,
            branch_rise=float(shape_settings["branch_rise"]),
            triad_canopy=bool(shape_settings["triad_canopy"]),
        )

        branch_points = list(branch_data["points"])
        branch_radius = _lerp(radius_base * 0.55, radius_top * 0.95, float(level))
        wood_parts.extend(
            _build_broadleaf_branch_parts(
                bpy,
                asset_name=asset_name,
                branch_index=idx,
                anchor=anchor,
                branch_points=branch_points,
                branch_radius=branch_radius,
                triad_canopy=bool(shape_settings["triad_canopy"]),
                vertices=vertices,
            )
        )
        canopy_parts.extend(
            _build_broadleaf_branch_canopy_parts(
                bpy,
                asset_name=asset_name,
                branch_index=idx,
                tip=tuple(branch_data["tip"]),
                forward_source=tuple(branch_data["forward_source"]),
                triad_canopy=bool(shape_settings["triad_canopy"]),
                canopy_radius_base=float(shape_settings["canopy_radius_base"]),
                canopy_z_scale=float(shape_settings["canopy_z_scale"]),
                main_canopy_center=tuple(shape_settings["main_canopy_center"]),
                canopy_merge_pull=float(shape_settings["canopy_merge_pull"]),
                side_canopy_scales=list(shape_settings["side_canopy_scales"]),
                tip_connector_radius_scale=float(
                    shape_settings["tip_connector_radius_scale"]
                ),
                tip_connector_forward_offset=float(
                    shape_settings["tip_connector_forward_offset"]
                ),
                tip_connector_z_offset=float(shape_settings["tip_connector_z_offset"]),
                canopy_bridge_t=float(shape_settings["canopy_bridge_t"]),
                canopy_bridge_radius_scale=float(
                    shape_settings["canopy_bridge_radius_scale"]
                ),
            )
        )

    crown = _add_large_tree_canopy_poly(
        bpy,
        loc=tuple(shape_settings["main_canopy_center"]),
        radius=float(shape_settings["main_canopy_radius"]),
        scale_xyz=(
            1.2 if bool(shape_settings["triad_canopy"]) else 1.12,
            1.06 if bool(shape_settings["triad_canopy"]) else 1.12,
            float(shape_settings["canopy_z_scale"]),
        ),
        rotation_deg=(2.0, -3.0, 6.0)
        if bool(shape_settings["triad_canopy"])
        else (3.0, -4.0, 8.0),
        subdivisions=0 if bool(shape_settings["triad_canopy"]) else 1,
    )
    if crown is not None:
        crown.name = (
            f"{asset_name}_Canopy_Main"
            if bool(shape_settings["triad_canopy"])
            else f"{asset_name}_Canopy_Crown"
        )
        canopy_parts.append(crown)

    if len(wood_parts) == 0:
        return {
            "status": "error",
            "error": "Failed to create broadleaf trunk geometry.",
        }
    if len(canopy_parts) == 0:
        return {
            "status": "error",
            "error": "Failed to create broadleaf canopy geometry.",
        }

    return {
        "status": "ok",
        "woodParts": wood_parts,
        "canopyParts": canopy_parts,
    }


def _build_geometric_tree_parts(
    bpy,
    *,
    asset_name: str,
    spec: Dict[str, Any],
    leaves_mat: Any,
) -> Dict[str, Any]:
    kind = str(spec.get("kind", "broadleaf")).lower()
    if kind == "pine":
        return _build_geometric_pine_tree(
            bpy,
            asset_name=asset_name,
            spec=spec,
            leaves_mat=leaves_mat,
        )

    broadleaf_build = _build_geometric_broadleaf_tree(
        bpy,
        asset_name=asset_name,
        spec=spec,
    )
    if broadleaf_build.get("status") != "ok":
        return broadleaf_build

    wood_parts = list(broadleaf_build.get("woodParts", []))
    if bool(spec.get("fuse_wood_parts", False)):
        wood_parts = _fuse_tree_wood_parts(
            bpy,
            wood_parts=wood_parts,
            asset_name=asset_name,
            voxel_size=float(spec.get("fuse_voxel_size", 0.09)),
            decimate_ratio=float(spec.get("fuse_decimate_ratio", 0.72)),
            smooth_factor=float(spec.get("fuse_smooth_factor", 0.18)),
            smooth_iterations=int(spec.get("fuse_smooth_iterations", 6)),
        )

    return {
        "status": "ok",
        "woodParts": wood_parts,
        "canopyParts": list(broadleaf_build.get("canopyParts", [])),
    }


def _fuse_tree_wood_parts(
    bpy,
    *,
    wood_parts: List[Any],
    asset_name: str,
    voxel_size: float,
    decimate_ratio: float,
    smooth_factor: float,
    smooth_iterations: int,
) -> List[Any]:
    if len(wood_parts) <= 1:
        return wood_parts

    bpy.ops.object.select_all(action="DESELECT")
    for part in wood_parts:
        if part is not None:
            part.select_set(True)
    bpy.context.view_layer.objects.active = wood_parts[0]
    bpy.ops.object.join()

    fused = bpy.context.active_object
    if fused is None:
        return wood_parts

    fused.name = f"{asset_name}_WoodFused"

    try:
        remesh = fused.modifiers.new(name="WoodFuseRemesh", type="REMESH")
        remesh.mode = "VOXEL"
        remesh.voxel_size = max(0.02, float(voxel_size))
        remesh.use_smooth_shade = False
        bpy.ops.object.modifier_apply(modifier=remesh.name)
    except Exception:
        pass

    if float(smooth_factor) > 0.0 and int(smooth_iterations) > 0:
        try:
            smooth = fused.modifiers.new(name="WoodFuseSmooth", type="SMOOTH")
            smooth.factor = max(0.0, min(1.0, float(smooth_factor)))
            smooth.iterations = max(1, int(smooth_iterations))
            bpy.ops.object.modifier_apply(modifier=smooth.name)
        except Exception:
            pass

    ratio = max(0.08, min(1.0, float(decimate_ratio)))
    if ratio < 0.999:
        try:
            decimate = fused.modifiers.new(name="WoodFuseDecimate", type="DECIMATE")
            decimate.ratio = ratio
            bpy.ops.object.modifier_apply(modifier=decimate.name)
        except Exception:
            pass

    _apply_object_transforms(bpy, fused)
    return [fused]


def _add_large_tree_branch_between_points(
    bpy,
    *,
    name: str,
    base_point: Tuple[float, float, float],
    tip_point: Tuple[float, float, float],
    radius_base: float,
    radius_top: float,
    displace_strength: float = 0.045,
    displace_scale: float = 1.8,
) -> Any:
    dx = tip_point[0] - base_point[0]
    dy = tip_point[1] - base_point[1]
    dz = tip_point[2] - base_point[2]
    depth = math.sqrt(dx * dx + dy * dy + dz * dz)
    if depth <= 1e-4:
        return None

    location = (
        (base_point[0] + tip_point[0]) * 0.5,
        (base_point[1] + tip_point[1]) * 0.5,
        (base_point[2] + tip_point[2]) * 0.5,
    )

    rotation_deg = (0.0, 0.0, 0.0)
    try:
        import mathutils  # type: ignore

        direction = mathutils.Vector((dx, dy, dz)).normalized()
        quat = direction.to_track_quat("Z", "Y")
        euler = quat.to_euler()
        rotation_deg = (
            math.degrees(euler.x),
            math.degrees(euler.y),
            math.degrees(euler.z),
        )
    except Exception:
        # Fallback orientation; Blender runtime should normally hit mathutils path.
        yaw = math.degrees(math.atan2(dy, dx))
        rotation_deg = (52.0, 0.0, yaw)

    return _add_large_tree_trunk_segment(
        bpy,
        name=name,
        radius_base=radius_base,
        radius_top=radius_top,
        depth=depth,
        location=location,
        rotation_deg=rotation_deg,
        displace_strength=displace_strength,
        displace_scale=displace_scale,
    )


def _add_large_tree_trunk_parts(bpy, asset_name: str) -> List[Any]:
    parts = []

    parts.append(
        _add_large_tree_trunk_segment(
            bpy,
            name=f"{asset_name}_Trunk_Main",
            radius_base=0.52,
            radius_top=0.27,
            depth=8.9,
            location=(0.0, 0.0, 4.45),
            rotation_deg=(0.0, 0.0, 0.0),
            displace_strength=0.06,
            displace_scale=1.6,
        )
    )

    branch_specs = [
        {
            "name": f"{asset_name}_Branch_NE",
            "base_point": (0.05, 0.05, 6.5),
            "tip_point": (1.18, 0.50, 10.15),
            "radius_base": 0.35,
            "radius_top": 0.15,
            "displace_strength": 0.045,
            "displace_scale": 1.8,
        },
        {
            "name": f"{asset_name}_Branch_SW",
            "base_point": (-0.05, -0.05, 6.2),
            "tip_point": (-1.12, -0.38, 10.20),
            "radius_base": 0.35,
            "radius_top": 0.15,
            "displace_strength": 0.045,
            "displace_scale": 1.8,
        },
        {
            "name": f"{asset_name}_Branch_SE",
            "base_point": (0.05, -0.05, 6.8),
            "tip_point": (0.88, -0.98, 10.00),
            "radius_base": 0.30,
            "radius_top": 0.12,
            "displace_strength": 0.04,
            "displace_scale": 1.9,
        },
        {
            "name": f"{asset_name}_Branch_NW",
            "base_point": (-0.05, 0.05, 7.2),
            "tip_point": (-0.92, 1.00, 10.03),
            "radius_base": 0.25,
            "radius_top": 0.10,
            "displace_strength": 0.035,
            "displace_scale": 2.0,
        },
    ]

    for spec in branch_specs:
        parts.append(_add_large_tree_branch_between_points(bpy, **spec))

    return [p for p in parts if p is not None]


def _add_geometric_pine_layer(
    bpy,
    *,
    name: str,
    radius: float,
    depth: float,
    location: Tuple[float, float, float],
    vertices: int,
) -> Any:
    bpy.ops.object.select_all(action="DESELECT")
    bpy.ops.mesh.primitive_cone_add(
        vertices=max(3, int(vertices)),
        radius1=radius,
        radius2=0.0,
        depth=depth,
        location=location,
    )
    obj = bpy.context.active_object
    if obj is None:
        return None

    obj.name = name
    return obj


def _build_geometric_pine_tree(
    bpy,
    *,
    asset_name: str,
    spec: Dict[str, Any],
    leaves_mat: Any,
) -> Dict[str, Any]:
    trunk_spec = dict(spec.get("trunk", {}))
    profile = dict(spec.get("pine_profile", {}))

    radius_base = float(trunk_spec.get("radius_base", 0.18))
    radius_top = float(trunk_spec.get("radius_top", 0.11))
    depth = float(trunk_spec.get("depth", 6.0))
    center = tuple(trunk_spec.get("location", (0.0, 0.0, 3.0)))
    vertices = max(5, int(trunk_spec.get("vertices", 6)))

    trunk_base = (
        float(center[0]),
        float(center[1]),
        float(center[2]) - depth * 0.5,
    )
    lean = tuple(profile.get("lean", (0.08, -0.05)))
    trunk_top = (
        float(center[0]) + float(lean[0]),
        float(center[1]) + float(lean[1]),
        float(center[2]) + depth * 0.5,
    )

    trunk_points = _build_curved_trunk_points(
        base=trunk_base,
        top=trunk_top,
        segment_count=int(profile.get("trunk_segments", 6)),
        sway=float(profile.get("sway", 0.14)),
        sway_phase=float(profile.get("sway_phase", random.uniform(-0.6, 0.6))),
        s_curve_bias=float(profile.get("s_curve_bias", 0.2)),
    )
    wood_parts = _build_tapered_segment_chain(
        bpy,
        name_prefix=f"{asset_name}_Trunk",
        points=trunk_points,
        radius_base=radius_base * float(profile.get("base_flare", 1.2)),
        radius_top=radius_top,
        vertices=vertices,
    )
    if len(wood_parts) == 0:
        return {
            "status": "error",
            "error": "Failed to create geometric pine trunk.",
        }

    canopy_parts: List[Any] = []
    pine_layers = list(spec.get("pine_layers", []))
    if len(pine_layers) == 0:
        return {
            "status": "error",
            "error": "Pine variation is missing canopy layers.",
        }

    whorl_step = float(profile.get("whorl_step_deg", 137.5))
    whorl_phase = float(profile.get("whorl_phase_deg", 0.0))
    layer_pitch = float(profile.get("layer_pitch_deg", -4.0))

    for index, layer in enumerate(pine_layers):
        level = float(layer.get("level", 0.65 + index * 0.08))
        anchor = _sample_polyline_point(trunk_points, level)
        yaw_deg = float(layer.get("yaw_deg", whorl_phase + index * whorl_step))
        yaw = math.radians(yaw_deg)
        radial_offset = float(layer.get("radial_offset", 0.0))

        location = (
            anchor[0] + math.cos(yaw) * radial_offset,
            anchor[1] + math.sin(yaw) * radial_offset,
            anchor[2] + float(layer.get("z_offset", 0.0)),
        )
        obj = _add_geometric_pine_layer(
            bpy,
            name=f"{asset_name}_Canopy_{index}",
            radius=float(layer.get("radius", 1.0)),
            depth=float(layer.get("depth", 1.25)),
            location=location,
            vertices=int(layer.get("vertices", 5)),
        )
        if obj is None:
            continue

        obj.rotation_euler = (
            math.radians(float(layer.get("tilt_deg", layer_pitch))),
            0.0,
            math.radians(yaw_deg),
        )
        _apply_object_transforms(bpy, obj)
        _assign_single_material(obj, leaves_mat)
        canopy_parts.append(obj)

    if len(canopy_parts) == 0:
        return {
            "status": "error",
            "error": "Failed to create geometric pine canopy layers.",
        }

    return {"status": "ok", "woodParts": wood_parts, "canopyParts": canopy_parts}


def _resolve_large_tree_geometric_variation(variation_name: str) -> Dict[str, Any]:
    variations: Dict[str, Dict[str, Any]] = {
        "Seedling": {
            "kind": "broadleaf",
            "seed": 8101,
            "fuse_wood_parts": True,
            "fuse_voxel_size": 0.14,
            "fuse_decimate_ratio": 0.22,
            "fuse_smooth_factor": 0.1,
            "fuse_smooth_iterations": 2,
            "trunk": {
                "radius_base": 0.12,
                "radius_top": 0.075,
                "depth": 2.2,
                "location": (0.0, 0.0, 1.1),
                "vertices": 6,
            },
            "broadleaf_profile": {
                "triad_canopy": True,
                "lean": (0.05, -0.03),
                "sway": 0.08,
                "sway_phase": -0.25,
                "s_curve_bias": 0.2,
                "trunk_segments": 4,
                "branch_levels": [0.7],
                "branch_yaws": [232.0],
                "branch_lengths": [0.82],
                "branch_rise": 0.11,
                "canopy_radius": 0.42,
                "main_canopy_radius": 0.68,
                "main_canopy_offset": (0.06, -0.02, 0.2),
                "canopy_merge_pull": 0.22,
                "side_canopy_scales": [0.5],
                "canopy_z_scale": 0.84,
                "tip_connector_radius_scale": 0.78,
                "tip_connector_forward_offset": 0.0,
                "tip_connector_z_offset": 0.0,
                "canopy_bridge_t": 0.32,
                "canopy_bridge_radius_scale": 0.74,
            },
            "bark_rgba": (0.43, 0.3, 0.18, 1.0),
            "leaf_rgba": (0.44, 0.65, 0.29, 1.0),
            "lod_ratios": (0.66, 0.38, 0.22),
        },
        "Sapling": {
            "kind": "broadleaf",
            "seed": 8102,
            "fuse_wood_parts": True,
            "fuse_voxel_size": 0.15,
            "fuse_decimate_ratio": 0.19,
            "fuse_smooth_factor": 0.11,
            "fuse_smooth_iterations": 3,
            "trunk": {
                "radius_base": 0.16,
                "radius_top": 0.1,
                "depth": 3.4,
                "location": (0.0, 0.0, 1.7),
                "vertices": 6,
            },
            "broadleaf_profile": {
                "triad_canopy": True,
                "lean": (0.08, -0.05),
                "sway": 0.1,
                "sway_phase": 0.16,
                "s_curve_bias": 0.22,
                "trunk_segments": 5,
                "branch_levels": [0.66],
                "branch_yaws": [214.0],
                "branch_lengths": [1.18],
                "branch_rise": 0.1,
                "canopy_radius": 0.56,
                "main_canopy_radius": 0.95,
                "main_canopy_offset": (0.1, -0.03, 0.28),
                "canopy_merge_pull": 0.2,
                "side_canopy_scales": [0.66],
                "canopy_z_scale": 0.8,
                "tip_connector_radius_scale": 0.8,
                "tip_connector_forward_offset": 0.01,
                "tip_connector_z_offset": 0.0,
                "canopy_bridge_t": 0.34,
                "canopy_bridge_radius_scale": 0.76,
            },
            "bark_rgba": (0.43, 0.3, 0.18, 1.0),
            "leaf_rgba": (0.41, 0.62, 0.27, 1.0),
            "lod_ratios": (0.66, 0.37, 0.21),
        },
        "Young": {
            "kind": "broadleaf",
            "seed": 8103,
            "fuse_wood_parts": True,
            "fuse_voxel_size": 0.17,
            "fuse_decimate_ratio": 0.16,
            "fuse_smooth_factor": 0.12,
            "fuse_smooth_iterations": 3,
            "trunk": {
                "radius_base": 0.24,
                "radius_top": 0.15,
                "depth": 4.8,
                "location": (0.0, 0.0, 2.4),
                "vertices": 6,
            },
            "broadleaf_profile": {
                "triad_canopy": True,
                "lean": (0.14, -0.08),
                "sway": 0.13,
                "sway_phase": -0.4,
                "s_curve_bias": 0.28,
                "trunk_segments": 6,
                "branch_levels": [0.58, 0.76],
                "branch_yaws": [226.0, 34.0],
                "branch_lengths": [1.58, 1.34],
                "branch_rise": 0.09,
                "canopy_radius": 0.78,
                "main_canopy_radius": 1.3,
                "main_canopy_offset": (0.16, -0.05, 0.36),
                "canopy_merge_pull": 0.16,
                "side_canopy_scales": [0.88, 0.7],
                "canopy_z_scale": 0.76,
                "tip_connector_radius_scale": 0.84,
                "tip_connector_forward_offset": 0.015,
                "tip_connector_z_offset": 0.01,
                "canopy_bridge_t": 0.36,
                "canopy_bridge_radius_scale": 0.8,
            },
            "bark_rgba": (0.42, 0.28, 0.17, 1.0),
            "leaf_rgba": (0.39, 0.58, 0.25, 1.0),
            "lod_ratios": (0.65, 0.35, 0.19),
        },
        "Mature": {
            "kind": "broadleaf",
            "seed": 8104,
            "fuse_wood_parts": True,
            "fuse_voxel_size": 0.18,
            "fuse_decimate_ratio": 0.15,
            "fuse_smooth_factor": 0.12,
            "fuse_smooth_iterations": 3,
            "trunk": {
                "radius_base": 0.36,
                "radius_top": 0.24,
                "depth": 6.3,
                "location": (0.0, 0.0, 3.15),
                "vertices": 6,
            },
            "broadleaf_profile": {
                "triad_canopy": True,
                "lean": (0.22, -0.16),
                "sway": 0.19,
                "sway_phase": -0.62,
                "s_curve_bias": 0.36,
                "trunk_segments": 7,
                "branch_levels": [0.56, 0.74],
                "branch_yaws": [224.0, 18.0],
                "branch_lengths": [2.34, 1.8],
                "branch_rise": 0.082,
                "canopy_radius": 1.08,
                "main_canopy_radius": 1.86,
                "main_canopy_offset": (0.26, -0.1, 0.52),
                "canopy_merge_pull": 0.14,
                "side_canopy_scales": [1.12, 0.78],
                "canopy_z_scale": 0.72,
                "tip_connector_radius_scale": 0.88,
                "tip_connector_forward_offset": 0.02,
                "tip_connector_z_offset": 0.01,
                "canopy_bridge_t": 0.38,
                "canopy_bridge_radius_scale": 0.84,
            },
            "bark_rgba": (0.42, 0.28, 0.17, 1.0),
            "leaf_rgba": (0.37, 0.57, 0.24, 1.0),
            "lod_ratios": (0.65, 0.34, 0.18),
        },
        "Adult": {
            "kind": "broadleaf",
            "seed": 8105,
            "fuse_wood_parts": True,
            "fuse_voxel_size": 0.2,
            "fuse_decimate_ratio": 0.14,
            "fuse_smooth_factor": 0.1,
            "fuse_smooth_iterations": 4,
            "trunk": {
                "radius_base": 0.44,
                "radius_top": 0.28,
                "depth": 7.1,
                "location": (0.0, 0.0, 3.55),
                "vertices": 6,
            },
            "broadleaf_profile": {
                "triad_canopy": True,
                "lean": (0.3, -0.2),
                "sway": 0.23,
                "sway_phase": 0.18,
                "s_curve_bias": 0.42,
                "trunk_segments": 8,
                "branch_levels": [0.52, 0.66, 0.79],
                "branch_yaws": [238.0, 28.0, 128.0],
                "branch_lengths": [2.9, 2.52, 2.24],
                "branch_rise": 0.074,
                "canopy_radius": 1.34,
                "main_canopy_radius": 2.34,
                "main_canopy_offset": (0.24, -0.08, 0.62),
                "canopy_merge_pull": 0.18,
                "side_canopy_scales": [1.24, 1.04, 0.94],
                "canopy_z_scale": 0.68,
                "tip_connector_radius_scale": 0.92,
                "tip_connector_forward_offset": 0.02,
                "tip_connector_z_offset": 0.015,
                "canopy_bridge_t": 0.4,
                "canopy_bridge_radius_scale": 0.88,
            },
            "bark_rgba": (0.4, 0.27, 0.16, 1.0),
            "leaf_rgba": (0.33, 0.53, 0.21, 1.0),
            "lod_ratios": (0.62, 0.32, 0.17),
        },
    }

    requested = str(variation_name or "").strip()
    if requested in variations:
        return {"variation": requested, **variations[requested]}

    return {"variation": "Mature", **variations["Mature"]}


def _get_available_large_tree_variations() -> List[str]:
    return [
        "Seedling",
        "Sapling",
        "Young",
        "Mature",
        "Adult",
    ]


def _build_geometric_lowpoly_tree(
    bpy,
    *,
    asset_name: str,
    variation_name: str,
) -> Dict[str, Any]:
    spec = _resolve_large_tree_geometric_variation(variation_name)
    random.seed(int(spec.get("seed", 7301)))

    bark_mat = _build_tree_material(
        bpy=bpy,
        name=f"{asset_name}_Bark",
        rgba=tuple(spec.get("bark_rgba", (0.42, 0.28, 0.17, 1.0))),
        roughness=0.86,
        metallic=0.0,
    )
    leaves_mat = _build_tree_material(
        bpy=bpy,
        name=f"{asset_name}_Leaves",
        rgba=tuple(spec.get("leaf_rgba", (0.39, 0.58, 0.25, 1.0))),
        roughness=0.85,
        metallic=0.0,
    )

    parts_build = _build_geometric_tree_parts(
        bpy,
        asset_name=asset_name,
        spec=spec,
        leaves_mat=leaves_mat,
    )
    if parts_build.get("status") != "ok":
        return parts_build

    wood_parts = list(parts_build.get("woodParts", []))
    canopy_parts = list(parts_build.get("canopyParts", []))

    for wood in wood_parts:
        _assign_single_material(wood, bark_mat)
    for canopy in canopy_parts:
        _assign_single_material(canopy, leaves_mat)

    if len(wood_parts) == 0:
        return {"status": "error", "error": "Failed to create geometric tree trunk."}

    if len(canopy_parts) == 0:
        return {"status": "error", "error": "Failed to create geometric tree canopy."}

    pieces = wood_parts + canopy_parts
    return {
        "status": "ok",
        "variation": str(spec.get("variation", "Mature")),
        "lodRatios": tuple(spec.get("lod_ratios", (0.65, 0.34, 0.18))),
        "pieces": pieces,
    }


def _add_large_tree_canopy_poly(
    bpy,
    loc,
    radius,
    scale_xyz,
    rotation_deg: Tuple[float, float, float],
    subdivisions: int = 1,
) -> Any:
    bpy.ops.object.select_all(action="DESELECT")
    bpy.ops.mesh.primitive_ico_sphere_add(
        subdivisions=subdivisions,
        radius=radius,
        location=loc,
    )
    obj = bpy.context.active_object
    if obj is None:
        return None

    obj.scale = scale_xyz

    obj.rotation_euler = (
        math.radians(rotation_deg[0]),
        math.radians(rotation_deg[1]),
        math.radians(rotation_deg[2]),
    )

    return obj


def _duplicate_tree_variant(
    bpy, source_obj: Any, variant_name: str, ratio: float
) -> Any:
    bpy.ops.object.select_all(action="DESELECT")
    _ensure_active_object(bpy, source_obj)
    bpy.ops.object.duplicate()
    obj = bpy.context.active_object
    if obj is None:
        return None

    obj.name = variant_name
    _apply_decimate(bpy, obj, ratio=ratio)
    _apply_object_transforms(bpy, obj)
    return obj


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


def _is_legacy_large_tree_style(style_profile: str) -> bool:
    return style_profile.strip().lower() in {
        "stylizedlowpolyforestv2_cleancanopy",
        "legacy",
        "legacybroadleaf",
    }


def _build_legacy_large_tree(bpy, asset_name: str) -> Dict[str, Any]:
    random.seed(424242)
    trunk_parts = _add_large_tree_trunk_parts(bpy, asset_name=asset_name)
    if len(trunk_parts) == 0:
        return {"status": "error", "error": "Failed to create trunk mesh."}

    bark_mat = _build_tree_material(
        bpy=bpy,
        name=f"{asset_name}_Bark",
        rgba=(0.37, 0.26, 0.17, 1.0),
        roughness=0.92,
        metallic=0.0,
    )
    leaves_mat = _build_tree_material(
        bpy=bpy,
        name=f"{asset_name}_Leaves",
        rgba=(0.43, 0.58, 0.25, 1.0),
        roughness=0.88,
        metallic=0.0,
    )

    for trunk_part in trunk_parts:
        _assign_single_material(trunk_part, bark_mat)

    canopy_layout = [
        ((0.0, 0.05, 10.75), 2.35, (1.18, 1.12, 0.95), (3.0, -2.0, 12.0)),
        ((1.18, 0.50, 10.15), 1.60, (1.00, 0.95, 0.88), (2.0, 4.0, 24.0)),
        ((-1.12, -0.38, 10.20), 1.56, (0.98, 1.04, 0.90), (-3.0, 5.0, -18.0)),
        ((0.88, -0.98, 10.00), 1.42, (0.95, 0.98, 0.84), (4.0, -4.0, 34.0)),
        ((-0.92, 1.00, 10.03), 1.42, (0.95, 0.99, 0.84), (-4.0, 4.0, -30.0)),
        ((0.08, 0.02, 12.00), 1.26, (0.86, 0.88, 0.70), (0.0, -2.0, 9.0)),
        ((0.0, 0.0, 9.52), 1.34, (0.98, 0.98, 0.68), (0.0, 0.0, 0.0)),
    ]
    canopy_parts = [
        _add_large_tree_canopy_poly(
            bpy,
            loc=loc,
            radius=radius,
            scale_xyz=scale_xyz,
            rotation_deg=rotation_deg,
            subdivisions=1,
        )
        for (loc, radius, scale_xyz, rotation_deg) in canopy_layout
    ]

    for canopy in canopy_parts:
        _assign_single_material(canopy, leaves_mat)

    pieces = [p for p in trunk_parts if p is not None] + [
        p for p in canopy_parts if p is not None
    ]
    return {
        "status": "ok",
        "pieces": pieces,
        "variation": "LegacyBroadleaf",
        "lodRatios": (0.42, 0.18, 0.12),
    }


def _build_large_tree_base_object(
    bpy, *, asset_name: str, pieces: List[Any]
) -> Dict[str, Any]:
    if len(pieces) == 0:
        return {"status": "error", "error": "No tree geometry pieces were generated."}

    bpy.ops.object.select_all(action="DESELECT")
    for piece in pieces:
        piece.select_set(True)
    bpy.context.view_layer.objects.active = pieces[0]
    bpy.ops.object.join()

    lod0 = bpy.context.active_object
    if lod0 is None:
        return {"status": "error", "error": "Failed to join tree meshes for LOD0."}

    lod0.name = f"{asset_name}_LOD0"

    scene = bpy.context.scene
    scene.cursor.location = (0.0, 0.0, 0.0)
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR", center="MEDIAN")

    try:
        bpy.ops.object.shade_flat()
    except Exception:
        pass

    try:
        lod0.data.use_auto_smooth = False
    except Exception:
        pass

    _smart_uv_project(bpy, lod0)
    _apply_object_transforms(bpy, lod0)
    return {"status": "ok", "lod0": lod0}


def _create_large_tree_lods(
    bpy, *, asset_name: str, lod0: Any, lod_ratios: Tuple[float, float, float]
) -> Dict[str, Any]:
    lod1 = _duplicate_tree_variant(
        bpy, lod0, f"{asset_name}_LOD1", ratio=float(lod_ratios[0])
    )
    if lod1 is None:
        return {"status": "error", "error": "Failed to duplicate LOD1."}

    lod2 = _duplicate_tree_variant(
        bpy, lod0, f"{asset_name}_LOD2", ratio=float(lod_ratios[1])
    )
    if lod2 is None:
        return {"status": "error", "error": "Failed to duplicate LOD2."}

    col = _duplicate_tree_variant(
        bpy, lod2, f"{asset_name}_COL", ratio=float(lod_ratios[2])
    )
    if col is None:
        return {"status": "error", "error": "Failed to duplicate collider mesh."}

    bpy.ops.object.select_all(action="DESELECT")
    for obj in (lod0, lod1, lod2, col):
        obj.select_set(True)
    bpy.context.view_layer.objects.active = lod0

    return {"status": "ok", "lod1": lod1, "lod2": lod2, "col": col}


def _ensure_preview_asset_exists(
    bpy,
    *,
    asset_name: str,
    style_profile: str,
    variation_name: str,
) -> Dict[str, Any]:
    if bpy.data.objects.get(f"{asset_name}_LOD0") is not None:
        return {"status": "ok"}

    if asset_name.lower() == "largetree":
        return cmd_generate_large_tree(
            bpy,
            {
                "assetName": asset_name,
                "styleProfile": style_profile,
                "variation": variation_name,
            },
        )

    return cmd_generate_e2e_prop(bpy, {"assetName": asset_name})


def _summarize_object(obj: Any) -> Dict[str, Any]:
    mesh = getattr(obj, "data", None)
    tris = _count_tris(mesh) if mesh is not None else 0
    return {"name": obj.name, "type": obj.type, "triangles": tris}


def _generate_large_tree_asset(
    bpy,
    *,
    asset_name: str,
    style_profile: str,
    variation_name: str,
    reset_scene: bool,
) -> Dict[str, Any]:
    if reset_scene:
        cmd_reset_scene(bpy)

    tree_build = (
        _build_legacy_large_tree(bpy, asset_name)
        if _is_legacy_large_tree_style(style_profile)
        else _build_geometric_lowpoly_tree(
            bpy,
            asset_name=asset_name,
            variation_name=variation_name,
        )
    )
    if tree_build.get("status") == "error":
        return tree_build

    pieces = list(tree_build.get("pieces", []))
    variation_used = str(tree_build.get("variation", variation_name))
    lod_ratios = tuple(tree_build.get("lodRatios", (0.42, 0.18, 0.12)))

    base_build = _build_large_tree_base_object(
        bpy,
        asset_name=asset_name,
        pieces=pieces,
    )
    if base_build.get("status") == "error":
        return base_build
    lod0 = base_build.get("lod0")

    lod_build = _create_large_tree_lods(
        bpy,
        asset_name=asset_name,
        lod0=lod0,
        lod_ratios=lod_ratios,
    )
    if lod_build.get("status") == "error":
        return lod_build
    lod1 = lod_build.get("lod1")
    lod2 = lod_build.get("lod2")
    col = lod_build.get("col")

    object_refs = [o for o in (lod0, lod1, lod2, col) if o is not None]
    return {
        "status": "ok",
        "assetName": asset_name,
        "styleProfile": style_profile,
        "variation": variation_used,
        "availableVariations": _get_available_large_tree_variations(),
        "objects": [_summarize_object(o) for o in object_refs],
        "objectRefs": object_refs,
    }


def _translate_objects_xy(objects: List[Any], dx: float, dy: float) -> None:
    for obj in objects:
        if obj is None:
            continue
        obj.location.x += dx
        obj.location.y += dy


def _compute_objects_world_bounds(objects: List[Any]) -> Any:
    try:
        import mathutils  # type: ignore
    except Exception:
        return None

    min_v = None
    max_v = None
    for obj in objects:
        if obj is None:
            continue
        if getattr(obj, "type", "") != "MESH":
            continue
        for corner in obj.bound_box:
            world_corner = obj.matrix_world @ mathutils.Vector(corner)
            if min_v is None:
                min_v = world_corner.copy()
                max_v = world_corner.copy()
                continue
            min_v.x = min(min_v.x, world_corner.x)
            min_v.y = min(min_v.y, world_corner.y)
            min_v.z = min(min_v.z, world_corner.z)
            max_v.x = max(max_v.x, world_corner.x)
            max_v.y = max(max_v.y, world_corner.y)
            max_v.z = max(max_v.z, world_corner.z)

    if min_v is None or max_v is None:
        return None

    return {"min": min_v, "max": max_v}


def _resolve_large_tree_sheet_variations(params: Dict[str, Any]) -> List[str]:
    requested = params.get("variations")
    available = _get_available_large_tree_variations()
    requested_list = requested if isinstance(requested, list) else available

    resolved = [str(v).strip() for v in requested_list if str(v).strip() in available]
    return resolved if len(resolved) > 0 else available


def _position_large_tree_sheet_camera(
    cam_obj: Any, light_obj: Any, placed_objects: List[Any]
) -> None:
    bounds = _compute_objects_world_bounds(placed_objects)
    if bounds is None:
        cam_obj.location = (10.0, -10.0, 8.0)
        light_obj.location = (14.0, -14.0, 14.0)
        _aim_camera_at(cam_obj, None)
        return

    min_v = bounds["min"]
    max_v = bounds["max"]
    center = (min_v + max_v) * 0.5
    extent_xy = max(max_v.x - min_v.x, max_v.y - min_v.y)
    extent_z = max_v.z - min_v.z

    cam_obj.location = (
        center.x + max(8.0, extent_xy * 0.9),
        center.y - max(8.0, extent_xy * 1.05),
        center.z + max(5.0, extent_z * 0.8),
    )
    light_obj.location = (
        center.x + max(12.0, extent_xy * 0.8),
        center.y - max(12.0, extent_xy * 0.8),
        center.z + max(12.0, extent_z * 1.2),
    )

    try:
        import mathutils  # type: ignore

        _aim_camera_at(cam_obj, mathutils.Vector((center.x, center.y, center.z + 1.5)))
    except Exception:
        _aim_camera_at(cam_obj, None)


def cmd_render_large_tree_variation_sheet(
    bpy, params: Dict[str, Any]
) -> Dict[str, Any]:
    file_path = str(params.get("filePath") or "")
    if not file_path:
        return {"status": "error", "error": MISSING_FILE_PATH_ERROR}

    out_dir = os.path.dirname(file_path)
    if out_dir:
        os.makedirs(out_dir, exist_ok=True)

    style_profile = str(params.get("styleProfile") or "GeometricLowPoly")
    variations = _resolve_large_tree_sheet_variations(params)

    width = int(params.get("width") or 2048)
    height = int(params.get("height") or 1024)
    columns = max(1, int(params.get("columns") or 3))
    spacing = float(params.get("spacing") or 9.0)

    cmd_reset_scene(bpy)

    placed_objects: List[Any] = []
    variation_summaries: List[Dict[str, Any]] = []
    for idx, variation_name in enumerate(variations):
        build = _generate_large_tree_asset(
            bpy,
            asset_name=f"LargeTree_{variation_name}",
            style_profile=style_profile,
            variation_name=variation_name,
            reset_scene=False,
        )
        if build.get("status") != "ok":
            return build

        row = idx // columns
        col = idx % columns
        dx = float(col * spacing)
        dy = float(-row * spacing)

        object_refs = list(build.get("objectRefs", []))
        _translate_objects_xy(object_refs, dx=dx, dy=dy)

        placed_objects.extend(object_refs)
        variation_summaries.append(
            {
                "variation": build.get("variation"),
                "assetName": build.get("assetName"),
                "offset": {"x": dx, "y": dy},
            }
        )

    scene = bpy.context.scene
    cam_obj, light_obj = _ensure_preview_camera_and_light(bpy, scene)
    _position_large_tree_sheet_camera(cam_obj, light_obj, placed_objects)

    scene.camera = cam_obj
    _set_preview_render_settings(scene, file_path=file_path, width=width, height=height)
    _set_preview_world_background(bpy, scene)

    try:
        bpy.ops.render.render(write_still=True)
    except Exception as e:
        return {"status": "error", "error": f"Render failed: {e}"}

    return {
        "status": "ok",
        "filePath": file_path,
        "width": width,
        "height": height,
        "styleProfile": style_profile,
        "variations": variation_summaries,
    }


def cmd_generate_large_tree(bpy, params: Dict[str, Any]) -> Dict[str, Any]:
    """Generate a deterministic broadleaf tree with UVs, LOD0/1/2 and collider mesh.

    Output object naming (Unity side convention):
      - <AssetName>_LOD0 / _LOD1 / _LOD2
      - <AssetName>_COL (solid collider mesh)
    """

    asset_name = str(params.get("assetName") or "LargeTree")
    style_profile = str(params.get("styleProfile") or "GeometricLowPoly")
    variation_name = str(params.get("variation") or "Mature")

    build = _generate_large_tree_asset(
        bpy,
        asset_name=asset_name,
        style_profile=style_profile,
        variation_name=variation_name,
        reset_scene=True,
    )
    if build.get("status") != "ok":
        return build

    # Keep object refs internal-only; command responses must remain JSON-safe.
    build.pop("objectRefs", None)
    return build


def cmd_render_preview(bpy, params: Dict[str, Any]) -> Dict[str, Any]:
    asset_name = str(params.get("assetName") or "BlenderE2EProp")
    style_profile = str(params.get("styleProfile") or "GeometricLowPoly")
    variation_name = str(params.get("variation") or "Mature")
    file_path = str(params.get("filePath") or "")
    width = int(params.get("width") or 512)
    height = int(params.get("height") or 512)

    if not file_path:
        return {"status": "error", "error": MISSING_FILE_PATH_ERROR}

    out_dir = os.path.dirname(file_path)
    if out_dir:
        os.makedirs(out_dir, exist_ok=True)

    # Ensure prop exists; if not, generate it.
    gen = _ensure_preview_asset_exists(
        bpy,
        asset_name=asset_name,
        style_profile=style_profile,
        variation_name=variation_name,
    )
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
    if command == "generate_large_tree":
        return cmd_generate_large_tree(bpy, params)
    if command == "render_preview":
        return cmd_render_preview(bpy, params)
    if command == "render_large_tree_variation_sheet":
        return cmd_render_large_tree_variation_sheet(bpy, params)
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
