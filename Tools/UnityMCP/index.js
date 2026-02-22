#!/usr/bin/env node
import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { CallToolRequestSchema, ListToolsRequestSchema } from "@modelcontextprotocol/sdk/types.js";

const server = new Server({
  name: "unity-mcp-server",
  version: "1.0.0"
}, {
  capabilities: { tools: {} }
});

server.setRequestHandler(ListToolsRequestSchema, async () => {
  return {
    tools: [
      {
        name: "unity_execute_static_method",
        description: "Executes a static C# method in the Unity Editor. Use this to trigger Editor scripts, generate prefabs, build scenes, etc.",
        inputSchema: {
          type: "object",
          properties: {
            type: { 
              type: "string", 
              description: "Assembly-qualified type name (e.g., 'SurvivalRPG.Editor.MCPCommands, SurvivalRPG.Editor')" 
            },
            method: { 
              type: "string", 
              description: "Name of the static method to execute (e.g., 'GenerateAll')" 
            }
          },
          required: ["type", "method"]
        }
      },
      {
        name: "unity_generate_all",
        description: "Generates all assets, prefabs, and the main scene in Unity.",
        inputSchema: {
          type: "object",
          properties: {}
        }
      },
      {
        name: "unity_generate_terrain",
        description: "Generates the terrain in the current Unity scene.",
        inputSchema: {
          type: "object",
          properties: {}
        }
      },
      {
        name: "unity_build_project",
        description: "Builds the Unity project.",
        inputSchema: {
          type: "object",
          properties: {}
        }
      },
      {
        name: "unity_get_scene_hierarchy",
        description: "Gets the hierarchy of the current Unity scene.",
        inputSchema: {
          type: "object",
          properties: {}
        }
      }
    ]
  };
});

server.setRequestHandler(CallToolRequestSchema, async (request) => {
  let type = "";
  let method = "";

  if (request.params.name === "unity_execute_static_method") {
    type = request.params.arguments.type;
    method = request.params.arguments.method;
  } else if (request.params.name === "unity_generate_all") {
    type = "SurvivalRPG.Editor.MCPCommands, SurvivalRPG.Editor";
    method = "GenerateAll";
  } else if (request.params.name === "unity_generate_terrain") {
    type = "SurvivalRPG.Editor.MCPCommands, SurvivalRPG.Editor";
    method = "GenerateTerrain";
  } else if (request.params.name === "unity_build_project") {
    type = "SurvivalRPG.Editor.MCPCommands, SurvivalRPG.Editor";
    method = "BuildProject";
  } else if (request.params.name === "unity_get_scene_hierarchy") {
    type = "SurvivalRPG.Editor.MCPCommands, SurvivalRPG.Editor";
    method = "GetSceneHierarchy";
  } else {
    throw new Error("Tool not found");
  }

  try {
    const response = await fetch("http://localhost:8765/execute", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ type, method })
    });
    const data = await response.json();
    
    if (response.ok) {
      return { content: [{ type: "text", text: `Success: ${data.message}` }] };
    } else {
      return { content: [{ type: "text", text: `Error: ${data.message}` }], isError: true };
    }
  } catch (e) {
    return { 
      content: [{ type: "text", text: `Failed to connect to Unity. Is the Unity Editor open and running the UnityMCPServer script? Error: ${e.message}` }], 
      isError: true 
    };
  }
});

const transport = new StdioServerTransport();
server.connect(transport).catch(console.error);
