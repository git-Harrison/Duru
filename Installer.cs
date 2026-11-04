using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

// Duru 설치 파일: 앱 exe 를 품고 있다가 %LOCALAPPDATA%\Programs\Duru 에 설치한다.
// 화면은 ModernSetup (요즘 앱 스타일 한 장짜리 설치 창), 문구는 Windows 언어를 따른다.
static class Installer
{
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();

    [STAThread]
    static void Main(string[] args)
    {
        try { SetProcessDPIAware(); } catch { }
        if (args.Length > 0 && (args[0] == "/silent" || args[0] == "/S")) { Install(null, true, true); return; }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var w = new ModernSetup
        {
            Text = L.T("Install Duru", "Duru 설치", "Duru のインストール", "安装 Duru", "Instalar Duru"),
            AppName = "Duru",
            Tagline = L.T("View and edit Markdown,\nJSON, YAML and text", "Markdown · JSON · YAML · 텍스트를\n두루 보고 고치는 에디터", "Markdown · JSON · YAML · テキストを\n表示・編集", "查看和编辑\nMarkdown · JSON · YAML · 文本", "Visor y editor de Markdown,\nJSON, YAML y texto"),
            FontName = L.UiFont,
            Accent = Color.FromArgb(47, 111, 237),
            IsUpdate = File.Exists(Setup.InstalledExe),
        };
        using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("icon.png")) w.Logo = new Bitmap(Image.FromStream(s));
        try { w.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        w.T.Install = L.T("Install and open", "설치하고 시작하기", "インストールして開く", "安装并打开", "Instalar y abrir");
        w.T.Update = L.T("Update and open", "업데이트하고 시작하기", "更新して開く", "更新并打开", "Actualizar y abrir");
        w.T.Done = L.T("All set", "준비됐어요", "準備ができました", "准备就绪", "Todo listo");
        w.T.Failed = L.T("Installation failed", "설치하지 못했어요", "インストールできませんでした", "安装失败", "No se pudo instalar");
        w.T.Retry = L.T("Try again", "다시 시도", "再試行", "重试", "Reintentar");
        w.T.Starting = L.T("Getting ready…", "준비하는 중…", "準備中…", "正在准备…", "Preparando…");
        w.T.FinePrint = "v" + Setup.Version + " · " + L.T("Installs for your account only, no admin rights needed", "관리자 권한 없이 내 계정에만 설치돼요", "管理者権限なしで、このアカウントにのみインストールされます", "仅为当前帐户安装，无需管理员权限", "Se instala solo para tu cuenta, sin permisos de administrador");
        w.Options.Add(new SetupOption { Text = L.T("Create a desktop shortcut", "바탕화면에 바로가기 만들기", "デスクトップにショートカットを作成", "创建桌面快捷方式", "Crear acceso directo en el escritorio"), On = true });
        w.Options.Add(new SetupOption { Text = L.T("Open .md files with Duru", ".md 파일을 Duru로 열기", ".md ファイルを Duru で開く", "用 Duru 打开 .md 文件", "Abrir archivos .md con Duru"), On = true });
        w.InstallWork = (p, opts) => Install(p, opts[0], opts[1]);
        w.Launch = () => Process.Start(Setup.InstalledExe);
        Application.Run(w);
    }

    public static void Install(SetupProgress p, bool desktop, bool makeDefault)
    {
        Action<float, string> say = (v, m) => { if (p != null) { p(v, m); Thread.Sleep(250); } };
        string exe = Setup.InstalledExe;
        say(0.15f, L.T("Closing Duru…", "실행 중인 Duru를 닫는 중…", "実行中の Duru を閉じています…", "正在关闭 Duru…", "Cerrando Duru…"));
        foreach (var proc in Process.GetProcesses())
        {
            try { if (string.Equals(proc.MainModule.FileName, exe, StringComparison.OrdinalIgnoreCase)) { proc.Kill(); proc.WaitForExit(3000); } } catch { }
        }
        say(0.45f, L.T("Copying files…", "파일을 복사하는 중…", "ファイルをコピーしています…", "正在复制文件…", "Copiando archivos…"));
        Directory.CreateDirectory(Setup.InstallDir);
        using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Duru.exe"))
        using (var o = File.Create(exe)) s.CopyTo(o);
        say(0.75f, L.T("Adding to menus…", "메뉴와 바로가기를 등록하는 중…", "メニューとショートカットを登録しています…", "正在添加到菜单…", "Añadiendo a los menús…"));
        Setup.Register(exe, desktop, makeDefault);
        say(1f, L.T("Finishing…", "마무리하는 중…", "仕上げています…", "即将完成…", "Terminando…"));
    }
}
