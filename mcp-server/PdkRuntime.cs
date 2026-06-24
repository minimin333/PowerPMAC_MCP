using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace PowerPmacMcp
{
    /// <summary>
    /// Makes the in-place PDK runtime (ODT.* managed DLLs, cygwin1/DKeyLib native DLLs,
    /// and the CLLLicFile.lic license) loadable without copying them into our output, and
    /// locates the cross-compiler/rsync toolchain — all auto-detected so the server is portable
    /// across machines. MUST be called once, at the very start of Main, before any ODT type is touched.
    ///
    /// Detection order (each step skipped if the env var / path is already valid):
    ///   PDK:        POWERPMAC_PDK_HOME -> registry "PowerPMAC Development Kit" -> ...\PowerPMAC\*\PDK -> common paths
    ///   Compilers:  POWERPMAC_COMPILERS_HOME -> DTBUILDPATH -> registry "PowerPMAC IDE*Compiler*" -> C:\DeltaTau\PowerPMAC\Compilers
    /// </summary>
    internal static class PdkRuntime
    {
        public static string Home { get; private set; }

        private static string _compilers;
        private static bool _compilersResolved;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        public static void Init()
        {
            Home = ResolvePdkHome() ?? "";
            if (string.IsNullOrEmpty(Home))
                Log("WARNING: Power PMAC PDK not found. Set POWERPMAC_PDK_HOME or install the Power PMAC IDE/PDK — build/connect/download will fail.");

            // Resolve ODT.*, Renci.SshNet, Ninject, Prism, etc. from the PDK folder.
            AppDomain.CurrentDomain.AssemblyResolve += (sender, e) =>
            {
                try
                {
                    if (string.IsNullOrEmpty(Home)) return null;
                    string simpleName = new AssemblyName(e.Name).Name;
                    string candidate = Path.Combine(Home, simpleName + ".dll");
                    if (File.Exists(candidate))
                        return Assembly.LoadFrom(candidate);
                }
                catch (Exception ex)
                {
                    Log("AssemblyResolve failed for " + e.Name + ": " + ex.Message);
                }
                return null;
            };

            // IMPORTANT: do NOT call SetDllDirectory(PDK_HOME) globally. Doing so makes the
            // in-process compile task load PDK_Reference's cygwin1.dll (a different version than
            // the Compilers' one), which breaks the C build ("cannot create response file").
            // Native PDK DLLs (DKeyLib/EncPass/license) are only needed for live comm, so the
            // native search dir is enabled transiently around connect via NativeSearch(true/false).

            // The C build runs cygwin make + the ARM cross-compiler (in ...\Compilers\usr\local\bin)
            // as child processes. Put those dirs on PATH (safety net) so make's recipe shell finds them.
            // Do NOT change CWD and do NOT add PDK_HOME to PATH (space-containing path broke it).
            string comp = CompilersHome();
            if (!string.IsNullOrEmpty(comp))
            {
                string dt = Path.Combine(comp, "bin") + ";" + Path.Combine(comp, "usr", "local", "bin");
                Environment.SetEnvironmentVariable(
                    "PATH", dt + ";" + Environment.GetEnvironmentVariable("PATH"));
            }
            else
            {
                Log("WARNING: Power PMAC Compilers not found — C builds / download may fail.");
            }

            Log("PDK_HOME = " + (string.IsNullOrEmpty(Home) ? "(not found)" : Home));
        }

        /// <summary>
        /// Enable/disable PDK_HOME as the native DLL search dir. Use ONLY transiently around
        /// the live-comm connect (DKeyLib/EncPass/license); leaving it on breaks the C build.
        /// </summary>
        public static void NativeSearch(bool on)
        {
            SetDllDirectory(on && !string.IsNullOrEmpty(Home) ? Home : null);
        }

        // ---- PDK location -------------------------------------------------
        public static string ResolvePdkHome()
        {
            string env = Environment.GetEnvironmentVariable("POWERPMAC_PDK_HOME");
            if (IsPdk(env)) return env.TrimEnd('\\', '"');

            string reg = RegistryInstallLocation(
                n => n.Equals("PowerPMAC Development Kit", StringComparison.OrdinalIgnoreCase), IsPdk);
            if (reg != null) return reg;

            // The PDK ships under the IDE: ...\PowerPMAC\<ver>\PDK (Compilers is at ...\PowerPMAC\Compilers).
            string comp = CompilersHome();
            if (!string.IsNullOrEmpty(comp))
            {
                string found = NewestPdkUnder(Path.GetDirectoryName(comp));
                if (found != null) return found;
            }
            foreach (var root in new[] { @"C:\DeltaTau\PowerPMAC", @"C:\Program Files (x86)\Delta Tau\Power PMAC" })
            {
                string found = NewestPdkUnder(root);
                if (found != null) return found;
            }
            return null;
        }

        private static string NewestPdkUnder(string ppRoot)
        {
            try
            {
                if (string.IsNullOrEmpty(ppRoot) || !Directory.Exists(ppRoot)) return null;
                if (IsPdk(Path.Combine(ppRoot, "PDK"))) return Path.Combine(ppRoot, "PDK");
                foreach (var verDir in Directory.GetDirectories(ppRoot).OrderByDescending(d => d, StringComparer.OrdinalIgnoreCase))
                {
                    string pdk = Path.Combine(verDir, "PDK");
                    if (IsPdk(pdk)) return pdk;
                }
            }
            catch { }
            return null;
        }

        private static bool IsPdk(string p)
        {
            if (string.IsNullOrEmpty(p)) return false;
            p = p.TrimEnd('\\', '"');
            return Directory.Exists(p)
                && File.Exists(Path.Combine(p, "CLLLicFile.lic"))                       // license (distinguishes the real PDK)
                && File.Exists(Path.Combine(p, "ODT.PowerPmacBuildAndDownload.dll"))
                && File.Exists(Path.Combine(p, "PPMAC460CompileTask.dll"))
                && File.Exists(Path.Combine(p, "cygwin1.dll"));
        }

        // ---- Compilers / rsync toolchain ----------------------------------
        public static string CompilersHome()
        {
            if (_compilersResolved) return _compilers;
            _compilers = ResolveCompilers();
            _compilersResolved = true;
            return _compilers;
        }

        private static string ResolveCompilers()
        {
            string env = Environment.GetEnvironmentVariable("POWERPMAC_COMPILERS_HOME");
            if (HasRsync(env)) return env.TrimEnd('\\', '"');

            string dt = Environment.GetEnvironmentVariable("DTBUILDPATH");
            if (!string.IsNullOrEmpty(dt))
            {
                string first = dt.Split(';')[0].Trim();             // ...\Compilers\bin
                string root = Path.GetDirectoryName(first);          // ...\Compilers
                if (HasRsync(root)) return root;
            }
            string reg = RegistryInstallLocation(
                n => n.IndexOf("PowerPMAC", StringComparison.OrdinalIgnoreCase) >= 0
                  && n.IndexOf("Compiler", StringComparison.OrdinalIgnoreCase) >= 0, HasRsync);
            if (reg != null) return reg;

            const string def = @"C:\DeltaTau\PowerPMAC\Compilers";
            if (HasRsync(def)) return def;
            return null;
        }

        private static bool HasRsync(string c)
        {
            return !string.IsNullOrEmpty(c) && File.Exists(Path.Combine(c.TrimEnd('\\', '"'), "bin", "rsync.exe"));
        }

        // ---- registry helper (uninstall DisplayName -> InstallLocation, both reg views) ----
        private static string RegistryInstallLocation(Func<string, bool> nameMatch, Func<string, bool> validate)
        {
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                try
                {
                    using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                    using (var unins = hklm.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
                    {
                        if (unins == null) continue;
                        foreach (var subName in unins.GetSubKeyNames())
                        {
                            using (var k = unins.OpenSubKey(subName))
                            {
                                if (k == null) continue;
                                var name = k.GetValue("DisplayName") as string;
                                if (string.IsNullOrEmpty(name) || !nameMatch(name)) continue;
                                var loc = (k.GetValue("InstallLocation") as string);
                                if (string.IsNullOrEmpty(loc)) continue;
                                loc = loc.Trim().TrimEnd('\\', '"');
                                if (validate(loc)) return loc;
                            }
                        }
                    }
                }
                catch { }
            }
            return null;
        }

        /// <summary>Diagnostics go to stderr — stdout is reserved for the MCP protocol.</summary>
        public static void Log(string msg)
        {
            Console.Error.WriteLine("[powerpmac-mcp] " + msg);
        }
    }
}
