using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace LumenPomodoro.Launcher;

class Program
{
    static async Task<int> Main(string[] args)
    {
        Console.Title = "Lumen Pomodoro";

        var appDir = AppContext.BaseDirectory;
        var mainExe = Path.Combine(appDir, "app", "LumenPomodoro.exe");

        if (!File.Exists(mainExe))
        {
            Console.Error.WriteLine("找不到主程序: " + mainExe);
            Console.WriteLine("按任意键退出...");
            Console.ReadKey();
            return 1;
        }

        if (!IsRuntimeInstalled())
        {
            Console.WriteLine("正在检测 .NET 运行环境...");
            Console.WriteLine("需要安装 .NET 9 Desktop Runtime (~30MB)。正在下载...");

            if (!await DownloadAndInstallRuntimeAsync())
            {
                Console.Error.WriteLine("安装失败，请手动安装:");
                Console.WriteLine("https://dotnet.microsoft.com/en-us/download/dotnet/9.0");
                Console.WriteLine("按任意键退出...");
                Console.ReadKey();
                return 1;
            }
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = mainExe,
                UseShellExecute = true,
                WorkingDirectory = appDir,
            };

            using var proc = Process.Start(psi);
            if (proc == null)
            {
                Console.Error.WriteLine("启动主程序失败");
                Console.ReadKey();
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"启动失败: {ex.Message}");
            Console.ReadKey();
            return 1;
        }

        return 0;
    }

    private static bool IsRuntimeInstalled()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "--list-runtimes",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };

            using var proc = Process.Start(psi);
            if (proc == null) return false;

            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(3000);

            return output.Contains("Microsoft.WindowsDesktop.App 9.");
        }
        catch
        {
            // dotnet command not found — check directory directly
            var progFiles = Environment.GetEnvironmentVariable("ProgramFiles")
                ?? @"C:\Program Files";
            var runtimeDir = Path.Combine(progFiles, "dotnet", "shared",
                "Microsoft.WindowsDesktop.App");
            if (!Directory.Exists(runtimeDir)) return false;

            foreach (var dir in Directory.EnumerateDirectories(runtimeDir, "9.*"))
                return true;

            return false;
        }
    }

    private static async Task<bool> DownloadAndInstallRuntimeAsync()
    {
        try
        {
            // .NET 9.0 Windows Desktop Runtime x64 installer URL
            var url = "https://dotnetcli.azureedge.net/dotnet/WindowsDesktop/9.0.0/"
                + "windowsdesktop-runtime-9.0.0-win-x64.exe";

            var installerPath = Path.Combine(Path.GetTempPath(), "dotnet-runtime-installer.exe");

            using (var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) })
            using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                var total = response.Content.Headers.ContentLength ?? -1;

                using var stream = await response.Content.ReadAsStreamAsync();
                using var file = File.Create(installerPath);

                var buffer = new byte[81920];
                long readSoFar = 0;
                int read;
                while ((read = await stream.ReadAsync(buffer)) > 0)
                {
                    await file.WriteAsync(buffer, 0, read);
                    readSoFar += read;
                    if (total > 0)
                    {
                        var pct = (int)(readSoFar * 100 / total);
                        Console.Write($"\r下载中... {pct}%");
                    }
                }
                Console.WriteLine("\r下载完成           ");
            }

            Console.WriteLine("正在安装...");
            var psi = new ProcessStartInfo
            {
                FileName = installerPath,
                Arguments = "/install /quiet /norestart",
                UseShellExecute = true,
                CreateNoWindow = true,
            };

            using var proc = Process.Start(psi);
            if (proc == null) return false;

            // Wait up to 2 minutes for installation
            proc.WaitForExit(120_000);

            try { File.Delete(installerPath); } catch { }

            if (proc.ExitCode == 0 || proc.ExitCode == 3010) // 3010 = need reboot
            {
                Console.WriteLine("安装完成");
                return true;
            }

            Console.Error.WriteLine($"安装程序返回错误代码: {proc.ExitCode}");
            return false;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"下载或安装失败: {ex.Message}");
            return false;
        }
    }
}
