#!/usr/bin/env node
import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { CallToolRequestSchema, ListToolsRequestSchema } from "@modelcontextprotocol/sdk/types.js";
import { spawn } from "node:child_process";
import fs from "node:fs";
import path from "node:path";

const BRIDGE_PORT = Number.parseInt(process.env.BLENDER_MCP_PORT ?? "8766", 10);
const BRIDGE_URL = `http://127.0.0.1:${BRIDGE_PORT}`;
const WORKSPACE_ROOT = process.cwd();

const E2E_ASSET_NAME = "BlenderE2EProp";
const LARGE_TREE_ASSET_NAME = "LargeTree";
const LARGE_TREE_VARIATIONS = [
  "Seedling",
  "Sapling",
  "Young",
  "Mature",
  "Adult",
];

function getE2EOutputPaths() {
  const artRoot = path.join(WORKSPACE_ROOT, "Assets", "Art");
  const outDir = path.join(artRoot, "Environment", "Props", E2E_ASSET_NAME, E2E_ASSET_NAME);
  // Folder: Assets/Art/Environment/Props/BlenderE2EProp/BlenderE2EProp.*
  const assetFolder = path.dirname(outDir);
  return {
    assetFolder,
    fbxPathAbs: `${outDir}.fbx`,
    previewPathAbs: path.join(assetFolder, "Preview.png"),
    fbxPathRel: path.relative(WORKSPACE_ROOT, `${outDir}.fbx`),
    previewPathRel: path.relative(WORKSPACE_ROOT, path.join(assetFolder, "Preview.png")),
  };
}

function getLargeTreeOutputPaths() {
  const artRoot = path.join(WORKSPACE_ROOT, "Assets", "Art");
  const outDir = path.join(artRoot, "Environment", "Vegetation", LARGE_TREE_ASSET_NAME, LARGE_TREE_ASSET_NAME);
  // Folder: Assets/Art/Environment/Vegetation/LargeTree/LargeTree.*
  const assetFolder = path.dirname(outDir);
  return {
    assetFolder,
    fbxPathAbs: `${outDir}.fbx`,
    previewPathAbs: path.join(assetFolder, "Preview.png"),
    fbxPathRel: path.relative(WORKSPACE_ROOT, `${outDir}.fbx`),
    previewPathRel: path.relative(WORKSPACE_ROOT, path.join(assetFolder, "Preview.png")),
  };
}

function getLargeTreeVariationOutputPaths(variationName) {
  const safeVariation = String(variationName).replaceAll(/[^a-zA-Z0-9_-]/g, "_");
  const assetName = `${LARGE_TREE_ASSET_NAME}_${safeVariation}`;
  const artRoot = path.join(WORKSPACE_ROOT, "Assets", "Art");
  const assetFolder = path.join(
    artRoot,
    "Environment",
    "Vegetation",
    LARGE_TREE_ASSET_NAME,
    "Variants",
    safeVariation,
  );

  return {
    variation: safeVariation,
    assetName,
    assetFolder,
    fbxPathAbs: path.join(assetFolder, `${assetName}.fbx`),
    previewPathAbs: path.join(assetFolder, "Preview.png"),
    fbxPathRel: path.relative(WORKSPACE_ROOT, path.join(assetFolder, `${assetName}.fbx`)),
    previewPathRel: path.relative(WORKSPACE_ROOT, path.join(assetFolder, "Preview.png")),
  };
}

function getLargeTreeVariationSheetPaths() {
  const assetFolder = path.join(
    WORKSPACE_ROOT,
    "Assets",
    "Art",
    "Environment",
    "Vegetation",
    LARGE_TREE_ASSET_NAME,
    "Variants",
  );

  const sheetPathAbs = path.join(assetFolder, "LargeTreeVariationsSheet.png");
  return {
    assetFolder,
    sheetPathAbs,
    sheetPathRel: path.relative(WORKSPACE_ROOT, sheetPathAbs),
  };
}

function resolveLargeTreeVariationList(rawVariations) {
  if (!Array.isArray(rawVariations) || rawVariations.length === 0) {
    return [...LARGE_TREE_VARIATIONS];
  }

  const filtered = rawVariations
    .map((v) => String(v).trim())
    .filter((v) => LARGE_TREE_VARIATIONS.includes(v));

  return filtered.length > 0 ? [...new Set(filtered)] : [...LARGE_TREE_VARIATIONS];
}

async function tryHandleLargeTreeVariationBatchTool(name, args) {
  if (name !== "blender_generate_large_tree_variations") {
    return null;
  }

  const styleProfile = args.styleProfile;
  const variations = resolveLargeTreeVariationList(args.variations);
  const perVariation = [];

  for (const variation of variations) {
    const paths = getLargeTreeVariationOutputPaths(variation);
    fs.mkdirSync(paths.assetFolder, { recursive: true });

    const generation = await executeBridgeCommand("generate_large_tree", {
      assetName: paths.assetName,
      styleProfile,
      variation,
    });

    const exportResult = await executeBridgeCommand("export_fbx", {
      filePath: paths.fbxPathAbs,
      selectedOnly: true,
    });

    const previewResult = await executeBridgeCommand("render_preview", {
      assetName: paths.assetName,
      styleProfile,
      variation,
      filePath: paths.previewPathAbs,
      width: 512,
      height: 512,
    });

    perVariation.push({
      variation,
      assetName: paths.assetName,
      generation,
      export: { ...exportResult, unityRelativePath: paths.fbxPathRel },
      preview: { ...previewResult, unityRelativePath: paths.previewPathRel },
    });
  }

  const sheetPaths = getLargeTreeVariationSheetPaths();
  fs.mkdirSync(sheetPaths.assetFolder, { recursive: true });
  const sheet = await executeBridgeCommand("render_large_tree_variation_sheet", {
    filePath: sheetPaths.sheetPathAbs,
    styleProfile,
    variations,
    width: args.sheetWidth,
    height: args.sheetHeight,
    columns: args.sheetColumns,
  });

  return toolResponseJson({
    status: "ok",
    styleProfile: styleProfile ?? "GeometricLowPoly",
    availableVariations: LARGE_TREE_VARIATIONS,
    generatedVariations: variations,
    outputs: perVariation,
    sheet: {
      ...sheet,
      unityRelativePath: sheetPaths.sheetPathRel,
    },
  });
}

async function tryHandleLargeTreeCoreTools(name, args) {
  if (name === "blender_generate_large_tree") {
    const paths = getLargeTreeOutputPaths();
    fs.mkdirSync(paths.assetFolder, { recursive: true });

    const gen = await executeBridgeCommand("generate_large_tree", {
      assetName: LARGE_TREE_ASSET_NAME,
      styleProfile: args.styleProfile,
      variation: args.variation,
    });
    const exp = await executeBridgeCommand("export_fbx", {
      filePath: paths.fbxPathAbs,
      selectedOnly: true,
    });

    return toolResponseJson({
      ...gen,
      export: exp,
      unityRelativePath: paths.fbxPathRel,
    });
  }

  if (name === "blender_render_large_tree_preview") {
    const paths = getLargeTreeOutputPaths();
    fs.mkdirSync(paths.assetFolder, { recursive: true });

    const res = await executeBridgeCommand("render_preview", {
      assetName: LARGE_TREE_ASSET_NAME,
      styleProfile: args.styleProfile,
      variation: args.variation,
      filePath: paths.previewPathAbs,
      width: 512,
      height: 512,
    });

    return toolResponseJson({
      ...res,
      unityRelativePath: paths.previewPathRel,
    });
  }

  return null;
}

let blenderProcess = null;
let blenderLaunching = null;

function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

function looksLikeWindows() {
  return process.platform === "win32";
}

function fileExists(p) {
  try {
    return fs.existsSync(p);
  } catch {
    return false;
  }
}

function getWindowsBlenderExecutableCandidates() {
  if (!looksLikeWindows()) {
    return [];
  }

  const base = String.raw`C:\Program Files\Blender Foundation`;
  if (!fileExists(base)) {
    return [];
  }

  try {
    return fs
      .readdirSync(base, { withFileTypes: true })
      .filter((entry) => entry.isDirectory())
      .map((entry) => path.join(base, entry.name, "blender.exe"))
      .filter((exePath) => fileExists(exePath));
  } catch {
    return [];
  }
}

function findBlenderExecutable() {
  const explicitBlenderPath = process.env.BLENDER_PATH;
  if (explicitBlenderPath && fileExists(explicitBlenderPath)) {
    return explicitBlenderPath;
  }

  const candidates = [...getWindowsBlenderExecutableCandidates(), "blender"];
  return candidates[0];
}

async function httpGetJson(url) {
  const res = await fetch(url, { method: "GET" });
  const text = await res.text();
  let json;
  try {
    json = JSON.parse(text);
  } catch {
    json = { raw: text };
  }
  return { ok: res.ok, status: res.status, json };
}

async function httpPostJson(url, body) {
  const res = await fetch(url, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  const text = await res.text();
  let json;
  try {
    json = JSON.parse(text);
  } catch {
    json = { raw: text };
  }
  return { ok: res.ok, status: res.status, json };
}

async function waitForBridgeReady(timeoutMs = 15000) {
  const deadline = Date.now() + timeoutMs;
  let lastError = null;

  while (Date.now() < deadline) {
    try {
      const { ok } = await httpGetJson(`${BRIDGE_URL}/health`);
      if (ok) return;
    } catch (e) {
      lastError = e;
    }
    await sleep(250);
  }

  const hint = lastError ? ` Last error: ${lastError.message}` : "";
  throw new Error(`Blender bridge not ready at ${BRIDGE_URL}/health.${hint}`);
}

async function ensureBlenderBridgeRunning() {
  if (process.env.BLENDER_MCP_NO_LAUNCH === "1") {
    throw new Error("BLENDER_MCP_NO_LAUNCH=1 is set; refusing to start Blender.");
  }

  if (blenderProcess?.exitCode === null) {
    return;
  }

  if (blenderLaunching) {
    await blenderLaunching;
    return;
  }

  blenderLaunching = (async () => {
    const blenderExe = findBlenderExecutable();
    const bridgeScript = path.join(WORKSPACE_ROOT, "Tools", "BlenderMCP", "blender_bridge.py");
    if (!fileExists(bridgeScript)) {
      throw new Error(`Missing bridge script at ${bridgeScript}`);
    }

    const args = [
      "--background",
      "--factory-startup",
      "--python",
      bridgeScript,
      "--",
      `--port=${BRIDGE_PORT}`,
    ];

    blenderProcess = spawn(blenderExe, args, {
      stdio: ["ignore", "pipe", "pipe"],
      cwd: WORKSPACE_ROOT,
      env: {
        ...process.env,
        BYTEWAR_WORKSPACE_ROOT: WORKSPACE_ROOT,
      },
    });

    blenderProcess.stdout.on("data", (d) => {
      // Keep stdout readable but not spammy; blender prints quite a lot.
      const s = d.toString();
      if (s.trim().length > 0) process.stderr.write(`[Blender] ${s}`);
    });
    blenderProcess.stderr.on("data", (d) => {
      const s = d.toString();
      if (s.trim().length > 0) process.stderr.write(`[Blender:stderr] ${s}`);
    });

    blenderProcess.on("exit", (code, signal) => {
      blenderProcess = null;
      blenderLaunching = null;
      process.stderr.write(`[Blender] exited code=${code} signal=${signal}\n`);
    });

    await waitForBridgeReady();
  })();

  try {
    await blenderLaunching;
  } finally {
    blenderLaunching = null;
  }
}

async function executeBridgeCommand(command, params = {}) {
  await ensureBlenderBridgeRunning();
  const { ok, status, json } = await httpPostJson(`${BRIDGE_URL}/execute`, { command, params });
  if (!ok) {
    const msg = json?.error ?? json?.message ?? `HTTP ${status}`;
    throw new Error(msg);
  }
  return json;
}

function toolResponseText(text) {
  return { content: [{ type: "text", text }] };
}

function toolResponseJson(obj) {
  return { content: [{ type: "text", text: JSON.stringify(obj, null, 2) }] };
}

const server = new Server(
  { name: "blender-mcp-server", version: "1.0.0" },
  { capabilities: { tools: {} } }
);

server.setRequestHandler(ListToolsRequestSchema, async () => {
  return {
    tools: [
      {
        name: "blender_status",
        description:
          "Reports Blender bridge status. Starts headless Blender on first use unless BLENDER_MCP_NO_LAUNCH=1.",
        inputSchema: { type: "object", properties: {} },
      },
      {
        name: "blender_reset_scene",
        description: "Deletes all objects in the current Blender scene.",
        inputSchema: { type: "object", properties: {} },
      },
      {
        name: "blender_list_objects",
        description: "Lists objects in the current Blender scene.",
        inputSchema: { type: "object", properties: {} },
      },
      {
        name: "blender_add_primitive",
        description:
          "Adds a primitive mesh to the scene (cube, uv_sphere, ico_sphere, cylinder, cone, plane).",
        inputSchema: {
          type: "object",
          properties: {
            type: {
              type: "string",
              enum: ["cube", "uv_sphere", "ico_sphere", "cylinder", "cone", "plane"],
            },
            name: { type: "string" },
            size: { type: "number", description: "Base size (default 1.0)" },
            location: {
              type: "array",
              items: { type: "number" },
              minItems: 3,
              maxItems: 3,
            },
            rotationEuler: {
              type: "array",
              items: { type: "number" },
              minItems: 3,
              maxItems: 3,
              description: "Radians",
            },
            scale: {
              type: "array",
              items: { type: "number" },
              minItems: 3,
              maxItems: 3,
            },
          },
          required: ["type"],
        },
      },
      {
        name: "blender_delete_object",
        description: "Deletes an object by name.",
        inputSchema: {
          type: "object",
          properties: { name: { type: "string" } },
          required: ["name"],
        },
      },
      {
        name: "blender_export_fbx",
        description: "Exports the current scene (or selection) to an FBX file.",
        inputSchema: {
          type: "object",
          properties: {
            filePath: { type: "string", description: "Absolute path to .fbx" },
            selectedOnly: { type: "boolean", default: false },
          },
          required: ["filePath"],
        },
      },
      {
        name: "blender_export_fbx_to_unity",
        description:
          "Exports to the Unity project under Assets/Art/... and returns the relative path. Does not commit any files.",
        inputSchema: {
          type: "object",
          properties: {
            category: {
              type: "string",
              enum: [
                "Characters/Player",
                "Characters/Enemies",
                "Characters/NPCs",
                "Environment/Props",
                "Environment/Rocks",
                "Environment/Vegetation",
                "Environment/Structures",
                "Weapons/Melee",
                "Weapons/Ranged",
                "Weapons/Shields",
                "Buildings",
                "Animations",
                "Premade",
              ],
            },
            assetName: { type: "string", description: "Folder and base filename" },
            selectedOnly: { type: "boolean", default: false },
          },
          required: ["category", "assetName"],
        },
      },
      {
        name: "blender_execute_script",
        description:
          "Executes a trusted Python snippet inside Blender. Use only for repo-local automation (no network access).",
        inputSchema: {
          type: "object",
          properties: { python: { type: "string" } },
          required: ["python"],
        },
      },
      {
        name: "blender_generate_e2e_prop",
        description:
          "Generates the deterministic BlenderE2EProp (UV unwrap + LOD0/1/2 + collider mesh) and exports it to Assets/Art/Environment/Props/BlenderE2EProp/BlenderE2EProp.fbx.",
        inputSchema: { type: "object", properties: {} },
      },
      {
        name: "blender_render_preview",
        description:
          "Renders a headless PNG preview of BlenderE2EProp to Assets/Art/Environment/Props/BlenderE2EProp/Preview.png.",
        inputSchema: { type: "object", properties: {} },
      },
      {
        name: "blender_generate_large_tree",
        description:
          "Generates a deterministic LargeTree using style/variation presets (default styleProfile=GeometricLowPoly) with UV unwrap, LOD0/1/2, and collider mesh; then exports it to Assets/Art/Environment/Vegetation/LargeTree/LargeTree.fbx.",
        inputSchema: {
          type: "object",
          properties: {
            styleProfile: {
              type: "string",
              description:
                "Tree style profile. Use GeometricLowPoly (default) for faceted low-poly trees, or LegacyBroadleaf/StylizedLowPolyForestV2_CleanCanopy for the older branch-heavy style.",
            },
            variation: {
              type: "string",
              description:
                "Variation preset name. GeometricLowPoly options: Seedling, Sapling, Young, Mature, Adult.",
            },
          },
        },
      },
      {
        name: "blender_render_large_tree_preview",
        description:
          "Renders a headless PNG preview of LargeTree to Assets/Art/Environment/Vegetation/LargeTree/Preview.png.",
        inputSchema: {
          type: "object",
          properties: {
            styleProfile: {
              type: "string",
              description:
                "Tree style profile to use if generation is needed before preview (default GeometricLowPoly).",
            },
            variation: {
              type: "string",
              description:
                "Variation preset to use if generation is needed before preview.",
            },
          },
        },
      },
      {
        name: "blender_generate_large_tree_variations",
        description:
          "Runs one deterministic generation+export pass for each requested LargeTree variation into Assets/Art/Environment/Vegetation/LargeTree/Variants/<Variation>/, renders per-variation previews, and creates a single contact-sheet preview image.",
        inputSchema: {
          type: "object",
          properties: {
            styleProfile: {
              type: "string",
              description:
                "Tree style profile to use for all generated variations. Defaults to GeometricLowPoly.",
            },
            variations: {
              type: "array",
              items: { type: "string" },
              description:
                "Optional subset of variation names to generate. Defaults to all GeometricLowPoly variations.",
            },
            sheetWidth: {
              type: "number",
              description: "Contact-sheet width in pixels (default 2048).",
            },
            sheetHeight: {
              type: "number",
              description: "Contact-sheet height in pixels (default 1024).",
            },
            sheetColumns: {
              type: "number",
              description: "Contact-sheet column count (default 3).",
            },
          },
        },
      },
    ],
  };
});

server.setRequestHandler(CallToolRequestSchema, async (request) => {
  try {
    const name = request.params.name;
    const args = request.params.arguments ?? {};

    if (name === "blender_status") {
      try {
        await ensureBlenderBridgeRunning();
        const status = await httpGetJson(`${BRIDGE_URL}/health`);
        return toolResponseJson({ bridgeUrl: BRIDGE_URL, ...status.json });
      } catch (e) {
        return {
          content: [
            {
              type: "text",
              text:
                `Blender not available. Set BLENDER_PATH or install Blender. ` +
                `Error: ${e.message}`,
            },
          ],
          isError: true,
        };
      }
    }

    if (name === "blender_reset_scene") {
      const res = await executeBridgeCommand("reset_scene", {});
      return toolResponseJson(res);
    }

    if (name === "blender_list_objects") {
      const res = await executeBridgeCommand("list_objects", {});
      return toolResponseJson(res);
    }

    if (name === "blender_add_primitive") {
      const res = await executeBridgeCommand("add_primitive", args);
      return toolResponseJson(res);
    }

    if (name === "blender_delete_object") {
      const res = await executeBridgeCommand("delete_object", args);
      return toolResponseJson(res);
    }

    if (name === "blender_export_fbx") {
      const res = await executeBridgeCommand("export_fbx", args);
      return toolResponseJson(res);
    }

    if (name === "blender_export_fbx_to_unity") {
      const { category, assetName, selectedOnly } = args;
      const safeAssetName = String(assetName).replaceAll(/[^a-zA-Z0-9_-]/g, "_");
      const artRoot = path.join(WORKSPACE_ROOT, "Assets", "Art");
      const outDir = path.join(artRoot, ...String(category).split("/"), safeAssetName);
      fs.mkdirSync(outDir, { recursive: true });
      const outPath = path.join(outDir, `${safeAssetName}.fbx`);
      const res = await executeBridgeCommand("export_fbx", {
        filePath: outPath,
        selectedOnly: Boolean(selectedOnly),
      });
      return toolResponseJson({ ...res, unityRelativePath: path.relative(WORKSPACE_ROOT, outPath) });
    }

    if (name === "blender_execute_script") {
      const res = await executeBridgeCommand("execute_script", args);
      return toolResponseJson(res);
    }

    if (name === "blender_generate_e2e_prop") {
      const paths = getE2EOutputPaths();
      fs.mkdirSync(paths.assetFolder, { recursive: true });

      const gen = await executeBridgeCommand("generate_e2e_prop", { assetName: E2E_ASSET_NAME });
      const exp = await executeBridgeCommand("export_fbx", {
        filePath: paths.fbxPathAbs,
        selectedOnly: true,
      });

      return toolResponseJson({
        ...gen,
        export: exp,
        unityRelativePath: paths.fbxPathRel,
      });
    }

    if (name === "blender_render_preview") {
      const paths = getE2EOutputPaths();
      fs.mkdirSync(paths.assetFolder, { recursive: true });

      const res = await executeBridgeCommand("render_preview", {
        assetName: E2E_ASSET_NAME,
        filePath: paths.previewPathAbs,
        width: 512,
        height: 512,
      });

      return toolResponseJson({
        ...res,
        unityRelativePath: paths.previewPathRel,
      });
    }

    const largeTreeCoreResponse = await tryHandleLargeTreeCoreTools(name, args);
    if (largeTreeCoreResponse) {
      return largeTreeCoreResponse;
    }

    const batchLargeTreeResponse = await tryHandleLargeTreeVariationBatchTool(name, args);
    if (batchLargeTreeResponse) {
      return batchLargeTreeResponse;
    }

    throw new Error("Tool not found");
  } catch (e) {
    return { content: [{ type: "text", text: `Error: ${e.message}` }], isError: true };
  }
});

function shutdown() {
  try {
    if (blenderProcess?.exitCode === null) {
      blenderProcess.kill();
    }
  } catch {
    // ignore
  }
}

process.on("SIGINT", shutdown);
process.on("SIGTERM", shutdown);
process.on("exit", shutdown);

const transport = new StdioServerTransport();
try {
  await server.connect(transport);
} catch (error) {
  console.error(error);
  process.exitCode = 1;
}
