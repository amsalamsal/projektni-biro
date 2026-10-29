using System;
using System.IO;
using System.Reflection;

namespace SiteDesignPro.AutoCAD.Infrastructure
{
    /// <summary>
    /// AutoCAD 2020-2024 run on .NET Framework, where acad.exe.config has no binding redirects for the
    /// System.Text.Json dependency chain (System.Memory, System.Buffers ...). This resolver loads those
    /// assemblies from the plug-in folder by simple name. It only answers for files that exist next to the
    /// plug-in, so it never replaces assemblies AutoCAD itself provides. Not needed on .NET 8+ (AutoCAD 2025+).
    /// </summary>
    internal static class DependencyResolver
    {
#if NETFRAMEWORK
        private static bool installed;
#endif

        public static void Install()
        {
#if NETFRAMEWORK
            if (installed)
            {
                return;
            }

            installed = true;
            string dir = Path.GetDirectoryName(typeof(DependencyResolver).Assembly.Location) ?? string.Empty;
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                string name = new AssemblyName(args.Name).Name ?? string.Empty;
                foreach (Assembly loaded in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (string.Equals(loaded.GetName().Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        return loaded;
                    }
                }

                string path = Path.Combine(dir, name + ".dll");
                return File.Exists(path) ? Assembly.LoadFrom(path) : null;
            };
#endif
        }
    }
}
