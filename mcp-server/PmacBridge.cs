using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using ODT.PowerPmacBuildAndDownload;
using ODT.PowerPmacComLib;

namespace PowerPmacMcp
{
    /// <summary>Result of a tool call: human/agent-readable text + error flag.</summary>
    internal sealed class ToolResult
    {
        public bool IsError;
        public string Text;
        public ToolResult(string text, bool isError = false) { Text = text; IsError = isError; }
    }

    /// <summary>
    /// Thin wrapper over the PDK for the headless-safe operations: local project build, and a
    /// persistent gpascii + terminal session for live command/response and shell access.
    ///
    /// Project DOWNLOAD is deliberately NOT here: the PDK's rsync download needs an interactive
    /// ssh terminal that a headless MCP process lacks, so it hangs. Use the standalone `ppmac-cli`
    /// console tool (run with a console) to build+download a project instead.
    /// </summary>
    internal sealed class PmacBridge
    {
        private ISyncGpasciiCommunicationInterface _gp;
        private ISyncTerminalCommunicationInterface _term;
        private string _connectedIp;
        private readonly object _lock = new object();

        // ---- Build (local) ------------------------------------------------
        public ToolResult BuildProject(string projectPath, bool debug)
        {
            if (string.IsNullOrEmpty(projectPath) || !File.Exists(projectPath))
                return new ToolResult("Project file not found: " + projectPath, true);

            var config = debug ? Build.BuildConfiguration.Debug : Build.BuildConfiguration.Release;
            var build = new Build();
            EventHandler<Build.BuildEventargs> handler = (s, e) => { };
            build.BuildMessages += handler;

            // PPMAC460CompileTask.dll (+ deps) are copied next to our exe via the csproj, so the
            // default 2-arg overload finds the MSBuild task in our BaseDirectory.
            Build.BuildResults r;
            try { r = build.BuildProject(projectPath, config); }
            catch (Exception ex) { return new ToolResult("Build threw: " + ex.Message, true); }
            finally { build.BuildMessages -= handler; }

            var sb = new StringBuilder();
            bool ok = r.TotalErrors == 0;
            sb.AppendLine("Build " + (ok ? "SUCCEEDED" : "FAILED") + " (" + (debug ? "Debug" : "Release") + ")");
            sb.AppendLine("Project: " + projectPath);
            sb.AppendLine("Errors: " + r.TotalErrors + "  Warnings: " + r.TotalWarnings);
            AppendList(sb, "ERRORS", r.Errors);
            AppendList(sb, "WARNINGS", r.Warnings);
            return new ToolResult(sb.ToString().TrimEnd(), !ok);
        }

        // ---- Download: rsync the project + run projpp on the controller ----
        // The PDK's managed Download.DownloadAllProgramsRSYNC hangs headless (its ssh needs a tty),
        // so we shell out to rsync+projpp via cmd. KEY: launch with a console (CreateNoWindow=false)
        // so cygwin sshpass/ssh get a PTY even though the MCP itself is a headless stdio process.
        // The project must be built first (build_project) so Bin/<config> holds the .out binaries.
        public ToolResult DownloadProject(string projectPath, string ip, string password)
        {
            if (string.IsNullOrEmpty(projectPath) || !File.Exists(projectPath))
                return new ToolResult("Project file not found: " + projectPath, true);
            if (string.IsNullOrEmpty(ip)) return new ToolResult("ipAddress is required.", true);
            if (string.IsNullOrEmpty(password)) password = "deltatau";
            string projectDir = Path.GetDirectoryName(projectPath);

            string compilers = Environment.GetEnvironmentVariable("POWERPMAC_COMPILERS_HOME");
            if (string.IsNullOrEmpty(compilers)) compilers = @"C:\DeltaTau\PowerPMAC\Compilers";
            if (!File.Exists(Path.Combine(compilers, "bin", "rsync.exe")))
                return new ToolResult("rsync toolchain not found under " + compilers + ".", true);

            string scriptPath = Path.Combine(Path.GetTempPath(), "ppmac_download.cmd");
            string outPath = Path.Combine(Path.GetTempPath(), "ppmac_download.out");
            try { File.WriteAllText(scriptPath, DownloadScript(compilers)); }
            catch (Exception ex) { return new ToolResult("Could not write download script: " + ex.Message, true); }

            try { if (File.Exists(outPath)) File.Delete(outPath); } catch { }
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                // The script writes its own output to outPath. UseShellExecute=true (ShellExecute)
                // allocates a NEW console for the console app even though the MCP is headless —
                // this is what gives cygwin sshpass/ssh a working PTY (plain Process.Start does not).
                Arguments = "/c \"\"" + scriptPath + "\" \"" + projectDir + "\" " + ip + " " + password +
                            " > \"" + outPath + "\" 2>&1\"",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            try
            {
                using (var p = Process.Start(psi))
                {
                    if (!p.WaitForExit(120000))
                    {
                        try { p.Kill(); } catch { }
                        return new ToolResult("Download timed out after 120s.", true);
                    }
                }
            }
            catch (Exception ex) { return new ToolResult("Download process failed: " + ex.Message, true); }

            string output = "";
            try { output = File.ReadAllText(outPath); } catch { }

            // projpp prints "Success: projpp" + PROJPP_EXIT=0 on success, or "...projpp errors = N"
            // + PROJPP_EXIT=1 on failure. (There is no "errors = 0" line on success.)
            int projppErrors = 0;
            bool rsyncOk = output.Contains("RSYNC_EXIT=0");
            foreach (var line in output.Replace("\r", "").Split('\n'))
            {
                int idx = line.IndexOf("projpp errors =", StringComparison.OrdinalIgnoreCase);
                if (idx >= 0) { int n; if (int.TryParse(line.Substring(idx + 15).Trim(), out n)) projppErrors = n; }
            }
            bool projppOk = output.Contains("PROJPP_EXIT=0") && projppErrors == 0;
            bool ok = rsyncOk && projppOk;

            var sb = new StringBuilder();
            sb.AppendLine("Download " + (ok ? "SUCCEEDED" : "FAILED") + " -> " + ip + " (rsync+projpp)");
            sb.AppendLine("Project: " + projectPath);
            if (!rsyncOk) sb.AppendLine("rsync did not report success — check the transfer.");
            if (projppErrors > 0) sb.AppendLine("projpp reported " + projppErrors + " error(s) — a script file failed to compile.");
            sb.AppendLine("-- log (tail) --");
            string[] lines = output.Replace("\r", "").Split('\n');
            int from = Math.Max(0, lines.Length - 40);
            for (int i = from; i < lines.Length; i++)
                if (lines[i].Trim().Length > 0) sb.AppendLine(lines[i]);
            return new ToolResult(sb.ToString().TrimEnd(), !ok);
        }

        // Generates the rsync+projpp batch. Run as: script.cmd "<projectDir>" <ip> <password>
        private static string DownloadScript(string compilers)
        {
            string bin = Path.Combine(compilers, "bin");
            string usr = Path.Combine(compilers, "usr", "local", "bin");
            var sb = new StringBuilder();
            sb.AppendLine("@echo off");
            sb.AppendLine("setlocal");
            sb.AppendLine("set \"PROJDIR=%~1\"");
            sb.AppendLine("set \"IP=%~2\"");
            sb.AppendLine("set \"PW=%~3\"");
            sb.AppendLine("if \"%PW%\"==\"\" set \"PW=deltatau\"");
            sb.AppendLine("set \"PATH=" + bin + ";" + usr + ";%PATH%\"");
            sb.AppendLine("set \"SSH=ssh -F /dev/null -o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null -o ConnectTimeout=15\"");
            // cd into the project and use a RELATIVE source: a Windows "C:\..." path makes rsync
            // treat the drive letter as a remote host ("source and destination both remote").
            sb.AppendLine("cd /d \"%PROJDIR%\"");
            sb.AppendLine("echo === RSYNC -^> %IP% ===");
            sb.AppendLine("sshpass -p %PW% rsync -rtz -s --rsh=\"%SSH%\" " +
                          "--exclude=\".vs\" --exclude=\".vscode\" --exclude=\"Temp\" --exclude=\"Log\" " +
                          "--exclude=\"*.log\" --exclude=\"*_pp_debug.txt\" --exclude=\"DownloadProgress.txt\" " +
                          "--exclude=\"errors.txt\" --exclude=\"warnings.txt\" --exclude=\"Exclude.txt\" " +
                          "--exclude=\"rsync-filter.txt\" --exclude=\"*.o\" \"./\" \"root@%IP%:/var/ftp/usrflash/Project/\"");
            sb.AppendLine("echo RSYNC_EXIT=%ERRORLEVEL%");
            sb.AppendLine("echo === PROJPP (load) ===");
            sb.AppendLine("sshpass -p %PW% %SSH% root@%IP% \"cd /var/ftp/usrflash/Project && projpp -l 2>&1\"");
            sb.AppendLine("echo PROJPP_EXIT=%ERRORLEVEL%");
            sb.AppendLine("endlocal");
            return sb.ToString();
        }

        // ---- Live gpascii + terminal session ------------------------------
        public ToolResult Connect(string ip, string user, string password, int port)
        {
            if (string.IsNullOrEmpty(ip)) return new ToolResult("ipAddress is required.", true);
            if (string.IsNullOrEmpty(user)) user = "root";
            if (string.IsNullOrEmpty(password)) password = "deltatau";
            if (port <= 0) port = 22;

            lock (_lock)
            {
                try
                {
                    Disconnect_NoLock();
                    // PDK_HOME native DLLs (DKeyLib/EncPass/license) are only needed during connect.
                    PdkRuntime.NativeSearch(true);
                    try { _gp = ODT.PowerPmacComLib.Connect.CreateSyncGpascii(CommunicationGlobals.ConnectionTypes.SSH, null); }
                    finally { PdkRuntime.NativeSearch(false); }
                    if (_gp == null)
                        return new ToolResult("CreateSyncGpascii returned null — check the PDK license (CLLLicFile.lic).", true);

                    bool ok = _gp.ConnectGpAscii(ip, port, user, password);
                    if (!ok)
                    {
                        _gp = null;
                        return new ToolResult("ConnectGpAscii failed to " + ip + ":" + port + " (user=" + user + ").", true);
                    }
                    _connectedIp = ip;
                    string resp;
                    _gp.GetResponse("echo 3", out resp);   // response without command echo (PDK sample)

                    // Also open a Linux shell terminal (best-effort) for exec_shell.
                    string termNote = "";
                    try
                    {
                        PdkRuntime.NativeSearch(true);
                        try { _term = ODT.PowerPmacComLib.Connect.CreateSyncTerminal(CommunicationGlobals.ConnectionTypes.SSH, null); }
                        finally { PdkRuntime.NativeSearch(false); }
                        if (_term == null || !_term.ConnectTerminal(ip, port, user, password))
                        {
                            _term = null;
                            termNote = " (shell terminal unavailable)";
                        }
                    }
                    catch { _term = null; termNote = " (shell terminal unavailable)"; }

                    return new ToolResult("Connected (gpascii" + (_term != null ? "+terminal" : "") +
                                          ") to " + ip + ":" + port + " as " + user + "." + termNote);
                }
                catch (Exception ex)
                {
                    _gp = null;
                    return new ToolResult("Connect threw: " + ex.Message, true);
                }
            }
        }

        public ToolResult Disconnect()
        {
            lock (_lock)
            {
                bool was = _gp != null;
                Disconnect_NoLock();
                return new ToolResult(was ? "Disconnected." : "Was not connected.");
            }
        }

        private void Disconnect_NoLock()
        {
            if (_gp != null) { try { _gp.DisconnectGpascii(); } catch { } _gp = null; }
            if (_term != null) { try { _term.DisconnectTerminal(); } catch { } _term = null; }
            _connectedIp = null;
        }

        public ToolResult Status()
        {
            lock (_lock)
            {
                bool connected = _gp != null && _gp.GpAsciiConnected;
                return new ToolResult(connected
                    ? "Connected to " + _connectedIp + " (gpascii" + (_term != null ? "+terminal" : "") + ")."
                    : "Not connected.");
            }
        }

        /// <summary>Send a command, returning the controller's response (or ack).</summary>
        public ToolResult GetResponse(string command)
        {
            if (string.IsNullOrEmpty(command)) return new ToolResult("command is required.", true);
            lock (_lock)
            {
                if (_gp == null || !_gp.GpAsciiConnected)
                    return new ToolResult("Not connected. Call connect first.", true);
                try
                {
                    string resp;
                    Status st = _gp.GetResponse(command, out resp);
                    if (st != ODT.PowerPmacComLib.Status.Ok)
                        return new ToolResult("Status=" + st + (resp != null ? ("\n" + resp) : ""), true);
                    return new ToolResult(resp == null ? "" : resp.TrimEnd());
                }
                catch (Exception ex) { return new ToolResult("GetResponse threw: " + ex.Message, true); }
            }
        }

        /// <summary>Send a command for its side effect (assignment / action).</summary>
        public ToolResult SendCommand(string command)
        {
            if (string.IsNullOrEmpty(command)) return new ToolResult("command is required.", true);
            lock (_lock)
            {
                if (_gp == null || !_gp.GpAsciiConnected)
                    return new ToolResult("Not connected. Call connect first.", true);
                try
                {
                    string resp;
                    Status st = _gp.GetResponse(command, out resp);
                    bool err = st != ODT.PowerPmacComLib.Status.Ok
                               || (resp != null && resp.IndexOf("ERR", StringComparison.OrdinalIgnoreCase) >= 0);
                    return new ToolResult("Status=" + st + (string.IsNullOrEmpty(resp) ? "" : ("\n" + resp.TrimEnd())), err);
                }
                catch (Exception ex) { return new ToolResult("SendCommand threw: " + ex.Message, true); }
            }
        }

        /// <summary>Batch read — one round-trip for many commands.</summary>
        public ToolResult GetResponses(List<string> commands)
        {
            if (commands == null || commands.Count == 0) return new ToolResult("commands array is required.", true);
            lock (_lock)
            {
                if (_gp == null || !_gp.GpAsciiConnected)
                    return new ToolResult("Not connected. Call connect first.", true);
                try
                {
                    List<string> resp;
                    Status st = _gp.GetResponse(commands, out resp);
                    var sb = new StringBuilder();
                    sb.AppendLine("Status=" + st);
                    if (resp != null)
                        for (int i = 0; i < resp.Count; i++)
                            sb.AppendLine(commands[Math.Min(i, commands.Count - 1)] + " => " + (resp[i] == null ? "" : resp[i].TrimEnd()));
                    return new ToolResult(sb.ToString().TrimEnd(), st != ODT.PowerPmacComLib.Status.Ok);
                }
                catch (Exception ex) { return new ToolResult("GetResponses threw: " + ex.Message, true); }
            }
        }

        /// <summary>Run a Linux shell command on the controller (via the SSH terminal).</summary>
        public ToolResult ExecShell(string command)
        {
            if (string.IsNullOrEmpty(command)) return new ToolResult("command is required.", true);
            lock (_lock)
            {
                if (_term == null)
                    return new ToolResult("No shell terminal. Call connect first (terminal opens with it).", true);
                try
                {
                    string resp;
                    Status st = _term.SendCommand(command, out resp);
                    if (st != ODT.PowerPmacComLib.Status.Ok)
                        return new ToolResult("Status=" + st + (resp != null ? ("\n" + resp) : ""), true);
                    return new ToolResult(resp == null ? "" : resp.TrimEnd());
                }
                catch (Exception ex) { return new ToolResult("ExecShell threw: " + ex.Message, true); }
            }
        }

        private static void AppendList(StringBuilder sb, string title, string[] items)
        {
            if (items == null || items.Length == 0) return;
            sb.AppendLine(title + ":");
            foreach (var it in items) sb.AppendLine("  " + it);
        }
    }
}
