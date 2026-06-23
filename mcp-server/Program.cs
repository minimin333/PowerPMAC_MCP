using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PowerPmacMcp
{
    internal static class Program
    {
        private const string ServerName = "powerpmac-mcp";
        private const string ServerVersion = "0.1.0";
        private const string DefaultProtocol = "2024-11-05";

        private static readonly PmacBridge Bridge = new PmacBridge();
        private static TextWriter _out;

        private static int Main(string[] args)
        {
            // Make the in-place PDK runtime loadable BEFORE any ODT type is touched.
            PdkRuntime.Init();

            // stdout is reserved for the JSON-RPC protocol. Capture the real stdout,
            // then redirect Console.Out to stderr so stray library prints can't corrupt it.
            var stdoutStream = Console.OpenStandardOutput();
            _out = new StreamWriter(stdoutStream, new UTF8Encoding(false)) { AutoFlush = true };
            Console.SetOut(Console.Error);

            var stdin = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false));
            PdkRuntime.Log("started (pid " + System.Diagnostics.Process.GetCurrentProcess().Id +
                           ", " + (IntPtr.Size == 4 ? "x86" : "x64") + ")");

            string line;
            while ((line = stdin.ReadLine()) != null)
            {
                line = line.Trim();
                if (line.Length == 0) continue;
                try { HandleLine(line); }
                catch (Exception ex) { PdkRuntime.Log("dispatch error: " + ex); }
            }
            return 0;
        }

        private static void HandleLine(string line)
        {
            JObject msg;
            try { msg = JObject.Parse(line); }
            catch (Exception ex) { PdkRuntime.Log("bad JSON: " + ex.Message); return; }

            string method = (string)msg["method"];
            JToken id = msg["id"];
            bool isNotification = id == null;

            if (method == null) return; // response/unknown — ignore

            switch (method)
            {
                case "initialize":
                    Reply(id, Initialize(msg["params"] as JObject));
                    break;
                case "tools/list":
                    Reply(id, new JObject { ["tools"] = ToolSchemas.All() });
                    break;
                case "tools/call":
                    Reply(id, CallTool(msg["params"] as JObject));
                    break;
                case "ping":
                    Reply(id, new JObject());
                    break;
                case "notifications/initialized":
                case "notifications/cancelled":
                    break; // notifications: no response
                default:
                    if (!isNotification)
                        ReplyError(id, -32601, "Method not found: " + method);
                    break;
            }
        }

        private static JObject Initialize(JObject p)
        {
            string proto = DefaultProtocol;
            if (p != null && p["protocolVersion"] != null) proto = (string)p["protocolVersion"];
            return new JObject
            {
                ["protocolVersion"] = proto,
                ["capabilities"] = new JObject { ["tools"] = new JObject() },
                ["serverInfo"] = new JObject { ["name"] = ServerName, ["version"] = ServerVersion }
            };
        }

        private static JObject CallTool(JObject p)
        {
            if (p == null) return ErrorContent("missing params");
            string name = (string)p["name"];
            JObject a = p["arguments"] as JObject ?? new JObject();

            ToolResult r;
            try
            {
                switch (name)
                {
                    case "build_project":
                        r = Bridge.BuildProject(Str(a, "projectPath"), IsDebug(a));
                        break;
                    case "download_project":
                        r = Bridge.DownloadProject(Str(a, "projectPath"), Str(a, "ipAddress"), Str(a, "password"));
                        break;
                    case "connect":
                        r = Bridge.Connect(Str(a, "ipAddress"), Str(a, "username"),
                                           Str(a, "password"), Int(a, "port", 22));
                        break;
                    case "disconnect":
                        r = Bridge.Disconnect();
                        break;
                    case "connection_status":
                        r = Bridge.Status();
                        break;
                    case "send_command":
                        r = Bridge.SendCommand(Str(a, "command"));
                        break;
                    case "get_response":
                        r = Bridge.GetResponse(Str(a, "command"));
                        break;
                    case "get_responses":
                        r = Bridge.GetResponses(StrList(a, "commands"));
                        break;
                    case "exec_shell":
                        r = Bridge.ExecShell(Str(a, "command"));
                        break;
                    default:
                        return ErrorContent("Unknown tool: " + name);
                }
            }
            catch (Exception ex) { r = new ToolResult("Tool threw: " + ex.Message, true); }

            return new JObject
            {
                ["content"] = new JArray { new JObject { ["type"] = "text", ["text"] = r.Text ?? "" } },
                ["isError"] = r.IsError
            };
        }

        // ---- helpers ----
        private static string Str(JObject a, string k) { return a[k] != null ? (string)a[k] : null; }
        private static int Int(JObject a, string k, int dflt) { return a[k] != null ? (int)a[k] : dflt; }
        private static bool IsDebug(JObject a)
        {
            string c = Str(a, "configuration");
            return c != null && c.Equals("Debug", StringComparison.OrdinalIgnoreCase);
        }
        private static List<string> StrList(JObject a, string k)
        {
            var list = new List<string>();
            if (a[k] is JArray arr) foreach (var t in arr) list.Add((string)t);
            return list;
        }

        private static JObject ErrorContent(string text)
        {
            return new JObject
            {
                ["content"] = new JArray { new JObject { ["type"] = "text", ["text"] = text } },
                ["isError"] = true
            };
        }

        private static void Reply(JToken id, JObject result)
        {
            if (id == null) return;
            Write(new JObject { ["jsonrpc"] = "2.0", ["id"] = id, ["result"] = result });
        }

        private static void ReplyError(JToken id, int code, string message)
        {
            Write(new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["error"] = new JObject { ["code"] = code, ["message"] = message }
            });
        }

        private static void Write(JObject o)
        {
            lock (_out)
            {
                _out.Write(o.ToString(Formatting.None));
                _out.Write('\n');
            }
        }
    }
}
