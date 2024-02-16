using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

namespace Nelderim.Launcher
{
    public static class Program
    {
        private static AssemblyLoadContext _loadContext;
        private static string? _rootDir;
        private static string _osDir = "";

        private static string GetOsIdentifier()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return "x64";
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return "osx";
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return "lib64";
            }
            return "unknown";
        }
        private static IntPtr ResolveUnmanagedDll(Assembly assembly, string unmanagedDllName)
        {
            var libraryPath = Path.Combine(_rootDir, _osDir, unmanagedDllName);

            Console.WriteLine($"Loading unmanaged DLL  {libraryPath}");

            if (File.Exists(libraryPath)) 
                return NativeLibrary.Load(libraryPath);
            
            Console.WriteLine($"ERROR: {libraryPath} doesn't exist");
            return IntPtr.Zero;
        }
        
        public static void Main(string[] args)
        {
            Console.WriteLine(RuntimeInformation.RuntimeIdentifier);
            _rootDir = AppContext.BaseDirectory;
            _osDir = GetOsIdentifier();
            _loadContext = AssemblyLoadContext.Default;
            _loadContext.ResolvingUnmanagedDll += ResolveUnmanagedDll;
            using (var game = new NelderimLauncher()) game.Run();
        }
    }
}