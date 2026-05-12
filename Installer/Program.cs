using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32;
using System.Windows.Forms;

namespace BarcodeFileFind.Setup;

internal static class Program
{
    private const string AppDisplayName = "바코드 파일 찾기";
    private const string AppExeName = "Barcode_File_Find.exe";
    private const string UninstallKeyName = "Barcode_File_Find";

    [STAThread]
    [SupportedOSPlatform("windows")]
    private static void Main(string[] args)
    {
        bool quiet = args.Any(arg =>
            arg.Equals("/quiet", StringComparison.OrdinalIgnoreCase) ||
            arg.Equals("/silent", StringComparison.OrdinalIgnoreCase));

        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string installDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs",
                "Barcode File Find");
            string appPath = Path.Combine(installDir, AppExeName);

            StopRunningApp();
            Directory.CreateDirectory(installDir);
            ExtractAppExecutable(appPath);
            WriteUninstallScript(installDir);
            CreateStartMenuShortcut(appPath);
            RegisterUninstallEntry(installDir, appPath);

            if (quiet)
            {
                return;
            }

            DialogResult result = MessageBox.Show(
                "설치가 완료되었습니다.\n\n시작 메뉴에서 '바코드 파일 찾기'로 검색해 실행할 수 있습니다.\n\n지금 실행할까요?",
                AppDisplayName,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);

            if (result == DialogResult.Yes)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = appPath,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            if (quiet)
            {
                Environment.ExitCode = 1;
                return;
            }

            MessageBox.Show(
                $"설치 중 오류가 발생했습니다.\n\n{ex.Message}",
                AppDisplayName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static void StopRunningApp()
    {
        foreach (Process process in Process.GetProcessesByName("Barcode_File_Find"))
        {
            process.Kill();
            process.WaitForExit(5000);
        }
    }

    private static void ExtractAppExecutable(string appPath)
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        string? resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(AppExeName, StringComparison.OrdinalIgnoreCase));

        if (resourceName == null)
        {
            throw new InvalidOperationException("설치 파일 안에서 실행 파일을 찾지 못했습니다.");
        }

        using Stream? resource = assembly.GetManifestResourceStream(resourceName);
        if (resource == null)
        {
            throw new InvalidOperationException("실행 파일 리소스를 열 수 없습니다.");
        }

        using FileStream output = File.Create(appPath);
        resource.CopyTo(output);
    }

    private static void CreateStartMenuShortcut(string appPath)
    {
        string programsDir = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        string shortcutDir = Path.Combine(programsDir, AppDisplayName);
        string shortcutPath = Path.Combine(shortcutDir, $"{AppDisplayName}.lnk");

        Directory.CreateDirectory(shortcutDir);

        Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType == null)
        {
            throw new InvalidOperationException("Windows 바로가기 생성 기능을 사용할 수 없습니다.");
        }

        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic shortcut = shell.CreateShortcut(shortcutPath);
        shortcut.TargetPath = appPath;
        shortcut.WorkingDirectory = Path.GetDirectoryName(appPath);
        shortcut.Description = "바코드/QR 스캔 값으로 EPS 파일을 찾아 Illustrator에서 여는 프로그램";
        shortcut.IconLocation = appPath;
        shortcut.Save();
    }

    private static void WriteUninstallScript(string installDir)
    {
        string scriptPath = Path.Combine(installDir, "Uninstall.ps1");
        string shortcutDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            AppDisplayName);

        string script = $$"""
            $ErrorActionPreference = 'SilentlyContinue'
            Get-Process -Name 'Barcode_File_Find' | Stop-Process -Force
            Remove-Item -LiteralPath '{{shortcutDir}}' -Recurse -Force
            Remove-Item -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{{UninstallKeyName}}' -Recurse -Force
            Start-Process -FilePath 'cmd.exe' -ArgumentList '/c timeout /t 1 > nul & rmdir /s /q "{{installDir}}"' -WindowStyle Hidden
            """;

        File.WriteAllText(scriptPath, script, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    private static void RegisterUninstallEntry(string installDir, string appPath)
    {
        string uninstallScript = Path.Combine(installDir, "Uninstall.ps1");
        string uninstallCommand = $"powershell.exe -ExecutionPolicy Bypass -File \"{uninstallScript}\"";

        using RegistryKey key = Registry.CurrentUser.CreateSubKey(
            $@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{UninstallKeyName}");

        key.SetValue("DisplayName", AppDisplayName);
        key.SetValue("DisplayVersion", "1.0.0");
        key.SetValue("Publisher", "rlatkdxo269-commits");
        key.SetValue("InstallLocation", installDir);
        key.SetValue("DisplayIcon", appPath);
        key.SetValue("UninstallString", uninstallCommand);
        key.SetValue("QuietUninstallString", uninstallCommand);
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
    }
}
