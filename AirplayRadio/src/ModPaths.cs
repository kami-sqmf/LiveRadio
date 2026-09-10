using System;
using System.IO;

namespace AirplayRadio
{
    internal static class ModPaths
    {
        internal static string NativeReceiver(string executableAssetPath)
        {
            // Cities II loads assemblies from bytes, so Assembly.Location can be empty.
            // The asset database owns the physical path for local and subscribed mods.
            if (string.IsNullOrWhiteSpace(executableAssetPath) || !Path.IsPathRooted(executableAssetPath))
                throw new InvalidOperationException("The game's mod asset has no absolute file path");
            string directory = Path.GetDirectoryName(executableAssetPath);
            if (string.IsNullOrEmpty(directory)) throw new InvalidOperationException("The game's mod asset has no directory");
            string native = Path.Combine(directory, "native", "AirplayRadioNative.dll");
            if (!File.Exists(native)) throw new FileNotFoundException("Airplay Radio receiver DLL is missing. Reinstall the complete mod folder.", native);
            return native;
        }
    }
}
