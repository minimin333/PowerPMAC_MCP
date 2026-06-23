using Newtonsoft.Json.Linq;

namespace PowerPmacMcp
{
    /// <summary>MCP tool definitions (name, description, JSON-Schema input).</summary>
    internal static class ToolSchemas
    {
        public static JArray All()
        {
            return new JArray
            {
                Tool("build_project",
                    "Build (compile) a Power PMAC project locally with the PDK. Returns errors/warnings. Does NOT touch a controller.",
                    Props(
                        Prop("projectPath", "string", "Absolute path to the project's .ppproj file."),
                        Enum("configuration", "Release or Debug (default Release).", "Release", "Debug")),
                    "projectPath"),

                Tool("download_project",
                    "Transfer a (already built) Power PMAC project to a controller (rsync) and load it (projpp). Run build_project first if C code changed.",
                    Props(
                        Prop("projectPath", "string", "Absolute path to the project's .ppproj file."),
                        Prop("ipAddress", "string", "Controller IP address, e.g. 192.168.0.200."),
                        Prop("password", "string", "SSH password for root (default deltatau).")),
                    "projectPath", "ipAddress"),

                Tool("connect",
                    "Open a persistent gpascii session to a controller for live command/response. Stays open across tool calls until disconnect.",
                    Props(
                        Prop("ipAddress", "string", "Controller IP address."),
                        Prop("username", "string", "SSH user (default root)."),
                        Prop("password", "string", "SSH password (default deltatau)."),
                        Prop("port", "integer", "SSH port (default 22).")),
                    "ipAddress"),

                Tool("disconnect", "Close the persistent gpascii session.", Props()),

                Tool("connection_status", "Report whether a gpascii session is currently open.", Props()),

                Tool("send_command",
                    "Send a gpascii command for its side effect (e.g. an assignment or action like 'Motor[1].JogSpeed=10' or '#1j+'). Returns the status/ack.",
                    Props(Prop("command", "string", "A Power PMAC on-line command.")),
                    "command"),

                Tool("get_response",
                    "Send a gpascii query and return the controller's response (e.g. 'Motor[1].ActPos' or 'Sys.ServoCount').",
                    Props(Prop("command", "string", "A Power PMAC query command.")),
                    "command"),

                Tool("get_responses",
                    "Send many gpascii queries in one round-trip and return all responses. Efficient for polling many elements.",
                    Props(StrArray("commands", "Array of Power PMAC query commands.")),
                    "commands"),

                Tool("exec_shell",
                    "Run a Linux shell command on the controller (e.g. 'ls /var/ftp/usrflash/Project'). Requires an active connection (the shell terminal opens with connect).",
                    Props(Prop("command", "string", "A Linux shell command to run on the controller.")),
                    "command")
            };
        }

        // ---- schema builders ----
        private static JObject Tool(string name, string desc, JObject properties, params string[] required)
        {
            var schema = new JObject { ["type"] = "object", ["properties"] = properties };
            if (required != null && required.Length > 0)
            {
                var req = new JArray();
                foreach (var r in required) req.Add(r);
                schema["required"] = req;
            }
            return new JObject
            {
                ["name"] = name,
                ["description"] = desc,
                ["inputSchema"] = schema
            };
        }

        private static JObject Props(params JProperty[] props)
        {
            var o = new JObject();
            foreach (var p in props) o.Add(p);
            return o;
        }

        private static JProperty Prop(string name, string type, string desc)
        {
            return new JProperty(name, new JObject { ["type"] = type, ["description"] = desc });
        }

        private static JProperty Enum(string name, string desc, params string[] values)
        {
            var arr = new JArray();
            foreach (var v in values) arr.Add(v);
            return new JProperty(name, new JObject { ["type"] = "string", ["description"] = desc, ["enum"] = arr });
        }

        private static JProperty StrArray(string name, string desc)
        {
            return new JProperty(name, new JObject
            {
                ["type"] = "array",
                ["description"] = desc,
                ["items"] = new JObject { ["type"] = "string" }
            });
        }
    }
}
