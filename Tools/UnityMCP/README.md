# Unity MCP Server

This is a Model Context Protocol (MCP) server that allows AI assistants to interact directly with the Unity Editor.

## Prerequisites

1. Node.js v18 or higher
2. Unity Editor running with the `ByteWar` project open.

## Setup

1. Install dependencies:

   ```bash
   npm install
   ```

2. Start the Unity Editor. The `UnityMCPServer` script will automatically start an HTTP server on `http://localhost:8765/`.
   - You can manually start/stop it from the Unity menu: `Tools > MCP Server > Start` / `Stop`.

## Usage with AI Assistants

To use this MCP server with an AI assistant (like Claude Desktop, Cursor, or VS Code with an MCP extension), you need to configure the assistant to start this server.

### Example Configuration (e.g., for Claude Desktop)

Add the following to your MCP configuration file (e.g., `claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "unity-mcp": {
      "command": "node",
      "args": ["C:/Projects/SurvivalRPG/Tools/UnityMCP/index.js"]
    }
  }
}
```

## Available Tools

- `unity_execute_static_method`: Executes any static C# method in the Unity Editor.
- `unity_generate_all`: Generates all assets, prefabs, and the main scene.
- `unity_generate_terrain`: Generates the terrain in the current scene.
- `unity_build_project`: Triggers a project build.
- `unity_get_scene_hierarchy`: Gets the hierarchy of the current Unity scene.

## How it Works

1. The AI assistant calls a tool provided by this Node.js MCP server.
2. The Node.js server sends an HTTP POST request to the Unity Editor (`http://localhost:8765/execute`).
3. The `UnityMCPServer.cs` script in Unity receives the request and uses Reflection to execute the requested static method on the main thread.
4. The result is returned back to the AI assistant.
