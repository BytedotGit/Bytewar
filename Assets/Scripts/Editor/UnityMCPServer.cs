using UnityEngine;
using UnityEditor;
using System;
using System.Net;
using System.Threading;
using System.IO;
using System.Text;
using System.Collections.Concurrent;
using System.Reflection;

namespace SurvivalRPG.Editor
{
    [InitializeOnLoad]
    public static class UnityMCPServer
    {
        private static HttpListener _listener;
        private static Thread _listenerThread;
        private static ConcurrentQueue<HttpListenerContext> _requestQueue = new ConcurrentQueue<HttpListenerContext>();

        [Serializable]
        private class ExecuteRequest
        {
            public string type;
            public string method;
        }

        static UnityMCPServer()
        {
            StartServer();
            EditorApplication.update += ProcessRequests;
            EditorApplication.quitting += StopServer;
        }

        [MenuItem("Tools/MCP/Start Server")]
        public static void ManualStartServer()
        {
            StartServer();
        }

        [MenuItem("Tools/MCP/Stop Server")]
        public static void ManualStopServer()
        {
            StopServer();
        }

        private static void StartServer()
        {
            if (_listener != null) return;

            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add("http://localhost:8765/");
                _listener.Start();

                _listenerThread = new Thread(Listen);
                _listenerThread.IsBackground = true;
                _listenerThread.Start();

                Debug.Log("[UnityMCPServer] Listening on http://localhost:8765/");
            }
            catch (Exception e)
            {
                Debug.LogError($"[UnityMCPServer] Failed to start: {e.Message}");
            }
        }

        private static void Listen()
        {
            while (_listener != null && _listener.IsListening)
            {
                try
                {
                    var context = _listener.GetContext();
                    _requestQueue.Enqueue(context);
                }
                catch { }
            }
        }

        private static void ProcessRequests()
        {
            while (_requestQueue.TryDequeue(out var context))
            {
                try
                {
                    var request = context.Request;
                    if (request.HttpMethod == "POST" && request.Url.AbsolutePath == "/execute")
                    {
                        using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                        {
                            string body = reader.ReadToEnd();
                            var data = JsonUtility.FromJson<ExecuteRequest>(body);

                            if (string.IsNullOrEmpty(data.type) || string.IsNullOrEmpty(data.method))
                            {
                                SendResponse(context, 400, "{\"status\":\"error\",\"message\":\"Missing type or method\"}");
                                continue;
                            }

                            Type type = Type.GetType(data.type);
                            if (type != null)
                            {
                                MethodInfo method = type.GetMethod(data.method, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                                if (method != null)
                                {
                                    // Execute on main thread
                                    EditorApplication.delayCall += () =>
                                    {
                                        try
                                        {
                                            object result = method.Invoke(null, null);
                                            string resultStr = result != null ? result.ToString() : "Method executed successfully";

                                            // Escape quotes and newlines for JSON
                                            string escapedResult = resultStr.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");

                                            SendResponse(context, 200, $"{{\"status\":\"success\",\"message\":\"{escapedResult}\"}}");
                                            Debug.Log($"[UnityMCPServer] Executed {data.type}.{data.method}");
                                        }
                                        catch (Exception e)
                                        {
                                            Debug.LogError($"[UnityMCPServer] Execution error: {e.Message}");
                                            SendResponse(context, 500, $"{{\"status\":\"error\",\"message\":\"{e.Message}\"}}");
                                        }
                                    };
                                }
                                else
                                {
                                    SendResponse(context, 404, "{\"status\":\"error\",\"message\":\"Method not found\"}");
                                }
                            }
                            else
                            {
                                SendResponse(context, 404, "{\"status\":\"error\",\"message\":\"Type not found\"}");
                            }
                        }
                    }
                    else
                    {
                        SendResponse(context, 404, "{\"status\":\"error\",\"message\":\"Endpoint not found\"}");
                    }
                }
                catch (Exception e)
                {
                    SendResponse(context, 500, $"{{\"status\":\"error\",\"message\":\"{e.Message}\"}}");
                }
            }
        }

        private static void SendResponse(HttpListenerContext context, int statusCode, string responseString)
        {
            try
            {
                var response = context.Response;
                response.StatusCode = statusCode;
                response.ContentType = "application/json";

                byte[] buffer = Encoding.UTF8.GetBytes(responseString);
                response.ContentLength64 = buffer.Length;

                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.OutputStream.Close();
            }
            catch { }
        }

        private static void StopServer()
        {
            if (_listener != null)
            {
                _listener.Stop();
                _listener.Close();
                _listener = null;
            }

            if (_listenerThread != null)
            {
                _listenerThread.Abort();
                _listenerThread = null;
            }
        }
    }
}