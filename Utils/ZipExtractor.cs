using System.IO;
using System.IO.Compression;

namespace SuperSMPLauncher.Utils;

public static class ZipExtractor
{
    public static void Extract(string zipPath, string targetDir)
    {
        if (Directory.Exists(targetDir) == false)
            Directory.CreateDirectory(targetDir);

        ZipFile.ExtractToDirectory(zipPath, targetDir, overwriteFiles: true);
    }
}