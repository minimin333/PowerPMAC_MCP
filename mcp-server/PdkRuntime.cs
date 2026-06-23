using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace PowerPmacMcp
{
    /// <summary>
    /// Makes the in-place PDK runtime (ODT.* managed DLLs, cygwin1/DKeyLib native DLLs,
    /// and the CLLLicFile.lic license) loadable without copying them into our output.
    /// MUST be called once, at the very start of Main, before any ODT type is touched.
    /// </summary>
    internal static class PdkRuntime
    {
        public static string Home { get; private set; }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        public static void Init()
        {
            Home = Environment.GetEnvironmentVariable("POWERPMAC_PDK_HOME");
            if (string.IsNullOrEmpty(Home))
                Home = @"C:\Cloude_Code\PowerPMAC_MCP\Power PMAC Manual\PDK_Reference";

            if (!Directory.Exists(Home))
                Log("WARNING: PDK_HOME does not exist: " + Home);

            // Resolve ODT.*, Renci.SshNet, Ninject, Prism, etc. from the PDK folder.
            AppDomain.CurrentDomain.AssemblyResolve += (sender, e) =>
            {
                try
                {
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
            // as child processes. Put those dirs (named by the machine var DTBUILDPATH) on PATH as
            // a safety net so make's recipe shell finds the compiler even if DTBUILDPATH is unset.
            // Do NOT change CWD and do NOT add PDK_HOME to PATH (space-containing path broke it).
            string dtbuild = Environment.GetEnvironmentVariable("DTBUILDPATH");
            if (string.IsNullOrEmpty(dtbuild))
            {
                string c = @"C:\DeltaTau\PowerPMAC\Compilers";
                dtbuild = Path.Combine(c, "bin") + ";" + Path.Combine(c, "usr", "local", "bin");
            }
            if (Directory.Exists(dtbuild.Split(';')[0]))
                Environment.SetEnvironmentVariable(
                    "PATH", dtbuild + ";" + Environment.GetEnvironmentVariable("PATH"));
            else
                Log("WARNING: compilers not found (DTBUILDPATH=" + dtbuild + ") — C builds may fail.");

            Log("PDK_HOME = " + Home);
        }

        /// <summary>
        /// Enable/disable PDK_HOME as the native DLL search dir. Use ONLY transiently around
        /// the live-comm connect (DKeyLib/EncPass/license); leaving it on breaks the C build.
        /// </summary>
        public static void NativeSearch(bool on)
        {
            SetDllDirectory(on ? Home : null);
        }

        /// <summary>Diagnostics go to stderr — stdout is reserved for the MCP protocol.</summary>
        public static void Log(string msg)
        {
            Console.Error.WriteLine("[powerpmac-mcp] " + msg);
        }
    }
}
