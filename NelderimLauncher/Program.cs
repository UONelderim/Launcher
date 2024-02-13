using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

namespace Nelderim.Launcher
{
    public static class Program
    {
        private static AssemblyLoadContext _loadContext;
        private static string? _rootDir;
        
        private static IntPtr ResolveUnmanagedDll(Assembly assembly, string unmanagedDllName)
        {
            Console.WriteLine($"Loading unmanaged DLL {unmanagedDllName} for {assembly.GetName().Name}");

            /* Try the correct native libs directory first */
            string osDir = "";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                osDir = "x64";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                osDir = "osx";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                osDir = "lib64";
            }

            var libraryPath = Path.Combine(_rootDir, osDir, unmanagedDllName);

            Console.WriteLine($"Resolved DLL to {libraryPath}");

            if (File.Exists(libraryPath))
                return NativeLibrary.Load(libraryPath);

            return IntPtr.Zero;
        }
        
        public static void Main(string[] args)
        {
            _rootDir = AppContext.BaseDirectory;
            _loadContext = AssemblyLoadContext.Default;
            _loadContext.ResolvingUnmanagedDll += ResolveUnmanagedDll;
            using (var game = new NelderimLauncher()) game.Run();
        }
    }
}