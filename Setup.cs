using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

// 설치/제거 시 레지스트리·바로가기 처리 (설치 파일과 앱이 같이 사용). 모두 현재 사용자(HKCU) 범위라 관리자 권한 불필요.
static class Setup
{
    public const string AppName = "Duru";
    public const string ExeName = "Duru.exe";
    public const string Version = "2.2.3";
    public const string AppId = "GitHarrison.Duru";
    const string ProgId = "Duru.doc";
    const string RegKey = @"Software\Duru";
    const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Duru";
    static readonly string[] Exts = { ".md", ".markdown", ".json", ".yaml", ".yml", ".txt" };
    static readonly string[] DefaultExts = { ".md", ".markdown" };   // 나머지는 기본 앱을 뺏지 않고 우클릭 메뉴만

    static string LocalAppData { get { return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData); } }
    public static string InstallDir { get { return Path.Combine(LocalAppData, @"Programs\Duru"); } }
    public static string InstalledExe { get { return Path.Combine(InstallDir, ExeName); } }
    // 아이콘 파일을 따로 두고 바로가기·앱 목록이 이 파일을 가리키게 한다 → 경로가 바뀌어 Windows 가 예전 아이콘 캐시를 못 씀
    public static string IconFile { get { return Path.Combine(InstallDir, "Duru.ico"); } }
    static string IconOf(string exe) { return File.Exists(IconFile) ? IconFile : exe; }
    static string StartMenuLnk { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppName + ".lnk"); } }
    static string DesktopLnk { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), AppName + ".lnk"); } }

    [DllImport("shell32.dll")] static extern void SHChangeNotify(int eventId, int flags, IntPtr a, IntPtr b);

    static void Set(string key, string name, object value)
    {
        using (var k = Registry.CurrentUser.CreateSubKey(key)) k.SetValue(name ?? "", value);
    }

    public static void Register(string exe, bool desktop, bool makeDefault)
    {
        RemoveLegacy();
        string open = "\"" + exe + "\" \"%1\"";
        Set(@"Software\Classes\" + ProgId, null, L.T("Duru document", "Duru 문서", "Duru ドキュメント", "Duru 文档", "Documento de Duru"));
        Set(@"Software\Classes\" + ProgId + @"\DefaultIcon", null, "\"" + IconOf(exe) + "\",0");
        Set(@"Software\Classes\" + ProgId + @"\shell\open\command", null, open);
        foreach (var ext in Exts)
        {
            Set(@"Software\Classes\" + ext + @"\OpenWithProgids", ProgId, "");
            Set(@"Software\Classes\SystemFileAssociations\" + ext + @"\shell\Duru", null, L.OpenWith);
            Set(@"Software\Classes\SystemFileAssociations\" + ext + @"\shell\Duru", "Icon", "\"" + exe + "\",0");
            Set(@"Software\Classes\SystemFileAssociations\" + ext + @"\shell\Duru\command", null, open);
            if (makeDefault && Array.IndexOf(DefaultExts, ext) >= 0) Set(@"Software\Classes\" + ext, null, ProgId);   // 다른 기본 앱이 지정돼 있지 않을 때 적용됨
        }
        Set(@"Software\Classes\Applications\" + ExeName, "FriendlyAppName", AppName);
        Set(@"Software\Classes\Applications\" + ExeName + @"\shell\open\command", null, open);
        foreach (var ext in Exts) Set(@"Software\Classes\Applications\" + ExeName + @"\SupportedTypes", ext, "");
        // Windows 설정 > 기본 앱 에 표시
        Set(RegKey + @"\Capabilities", "ApplicationName", AppName);
        Set(RegKey + @"\Capabilities", "ApplicationDescription", L.Tagline);
        foreach (var ext in Exts) Set(RegKey + @"\Capabilities\FileAssociations", ext, ProgId);
        Set(@"Software\RegisteredApplications", AppName, RegKey + @"\Capabilities");
        // 설정 > 앱 > 설치된 앱 에 표시
        Set(UninstallKey, "DisplayName", AppName);
        Set(UninstallKey, "DisplayIcon", "\"" + IconOf(exe) + "\",0");
        Set(UninstallKey, "DisplayVersion", Version);
        Set(UninstallKey, "Publisher", "Duru");
        Set(UninstallKey, "InstallLocation", Path.GetDirectoryName(exe));
        Set(UninstallKey, "UninstallString", "\"" + exe + "\" --uninstall");
        Set(UninstallKey, "QuietUninstallString", "\"" + exe + "\" --uninstall /S");
        Set(UninstallKey, "EstimatedSize", (int)(new FileInfo(exe).Length / 1024));
        Set(UninstallKey, "NoModify", 1);
        Set(UninstallKey, "NoRepair", 1);

        Shortcut(StartMenuLnk, exe);
        if (desktop) Shortcut(DesktopLnk, exe); else TryDelete(DesktopLnk);
        SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
    }

    public static void Unregister()
    {
        Remove(ProgId, "Duru", RegKey, UninstallKey, AppName, ExeName);
        TryDelete(StartMenuLnk);
        TryDelete(DesktopLnk);
        SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
    }

    static void Remove(string progId, string verb, string regKey, string uninstallKey, string appName, string exeName)
    {
        var cu = Registry.CurrentUser;
        cu.DeleteSubKeyTree(@"Software\Classes\" + progId, false);
        foreach (var ext in Exts)
        {
            using (var k = cu.OpenSubKey(@"Software\Classes\" + ext + @"\OpenWithProgids", true)) if (k != null) k.DeleteValue(progId, false);
            using (var k = cu.OpenSubKey(@"Software\Classes\" + ext, true)) if (k != null && (k.GetValue("") as string) == progId) k.DeleteValue("", false);
            cu.DeleteSubKeyTree(@"Software\Classes\SystemFileAssociations\" + ext + @"\shell\" + verb, false);
        }
        cu.DeleteSubKeyTree(@"Software\Classes\Applications\" + exeName, false);
        cu.DeleteSubKeyTree(regKey, false);
        using (var k = cu.OpenSubKey(@"Software\RegisteredApplications", true)) if (k != null) k.DeleteValue(appName, false);
        cu.DeleteSubKeyTree(uninstallKey, false);
    }

    // 예전 이름(MD 뷰어)으로 설치된 흔적 정리: 실행 중인 프로세스·레지스트리·바로가기·설치 폴더
    static void RemoveLegacy()
    {
        string oldDir = Path.Combine(LocalAppData, @"Programs\MdViewer");
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (p.MainModule.FileName.StartsWith(oldDir + "\\", StringComparison.OrdinalIgnoreCase)) { p.Kill(); p.WaitForExit(3000); }
            }
            catch { }
        }
        try { Remove("MdViewer.md", "MdViewer", @"Software\MdViewer", @"Software\Microsoft\Windows\CurrentVersion\Uninstall\MdViewer", "MD 뷰어", "MD뷰어.exe"); } catch { }
        TryDelete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "MD 뷰어.lnk"));
        TryDelete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "MD 뷰어.lnk"));
        try { if (Directory.Exists(oldDir)) Directory.Delete(oldDir, true); } catch { }
        try { string old = Path.Combine(LocalAppData, "MdViewer"); if (Directory.Exists(old)) Directory.Delete(old, true); } catch { }
    }

    static void Shortcut(string lnk, string exe)
    {
        TryDelete(lnk);
        dynamic ws = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
        dynamic s = ws.CreateShortcut(lnk);
        s.TargetPath = exe;
        s.WorkingDirectory = Path.GetDirectoryName(exe);
        s.IconLocation = IconOf(exe) + ",0";
        s.Description = L.Tagline;
        s.Save();
        TaskbarId.ApplyToShortcut(lnk, AppId);   // 작업 표시줄이 이 바로가기 아이콘을 쓰게
    }

    // Windows 아이콘 캐시 새로 고침 (Windows 10/11)
    static void RefreshIconCache()
    {
        try
        {
            var p = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "ie4uinit.exe"), "-show") { CreateNoWindow = true, UseShellExecute = false });
            p.WaitForExit(5000);
        }
        catch { }
    }

    static void TryDelete(string f) { try { if (File.Exists(f)) File.Delete(f); } catch { } }
}
