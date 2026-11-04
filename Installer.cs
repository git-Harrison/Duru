using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// Duru 설치 파일: 앱 exe 를 품고 있다가 %LOCALAPPDATA%\Programs\Duru 에 설치한다.
static class Installer
{
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();

    [STAThread]
    static void Main(string[] args)
    {
        try { SetProcessDPIAware(); } catch { }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        if (args.Length > 0 && (args[0] == "/silent" || args[0] == "/S")) { Install(true, true); return; }
        Application.Run(new SetupForm());
    }

    public static void Install(bool desktop, bool makeDefault)
    {
        string exe = Setup.InstalledExe;
        // 실행 중인 Duru 종료
        foreach (var p in Process.GetProcesses())
        {
            try { if (string.Equals(p.MainModule.FileName, exe, StringComparison.OrdinalIgnoreCase)) { p.Kill(); p.WaitForExit(3000); } } catch { }
        }
        Directory.CreateDirectory(Setup.InstallDir);
        using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Duru.exe"))
        using (var o = File.Create(exe)) s.CopyTo(o);
        Setup.Register(exe, desktop, makeDefault);
    }
}

class SetupForm : Form
{
    readonly Font fTitle = new Font(L.UiFont, 15f, FontStyle.Bold);
    readonly Font fBody = new Font(L.UiFont, 9.5f);
    readonly Font fSmall = new Font(L.UiFont, 8.5f);
    readonly Color accent = Color.FromArgb(47, 111, 237), muted = Color.FromArgb(110, 116, 128), ink = Color.FromArgb(31, 35, 41);
    const int W = 520, Pad = 32, TextW = W - Pad * 2;
    Panel page1, page2;
    CheckBox cbDesktop, cbDefault, cbRun;
    string installText = L.T("Install", "설치", "インストール", "安装", "Instalar");

    public SetupForm()
    {
        Text = L.T("Install Duru", "Duru 설치", "Duru のインストール", "安装 Duru", "Instalar Duru");
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.White;
        Font = fBody;

        Image logoImg;
        using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("icon.png")) logoImg = Image.FromStream(s);

        // 1페이지: 옵션
        page1 = new Panel { Location = Point.Empty };
        Header(page1, logoImg, Text, L.Tagline + "  (v" + Setup.Version + ")");
        int y = 116;
        y = Add(page1, Label(L.T("Install location", "설치 위치", "インストール先", "安装位置", "Ubicación"), fBody, muted, Pad, y)) + 4;
        y = Add(page1, Label(Setup.InstallDir, fBody, ink, Pad, y, TextW)) + 18;
        cbDesktop = Check(L.T("Create a desktop shortcut", "바탕화면에 바로가기 만들기", "デスクトップにショートカットを作成", "创建桌面快捷方式", "Crear un acceso directo en el escritorio"), Pad, y, true);
        y = Add(page1, cbDesktop) + 8;
        cbDefault = Check(L.T("Open .md files with Duru (if no other default app is set)", ".md 파일을 Duru로 열기 (다른 기본 앱이 없을 때)", ".md ファイルを Duru で開く (他の既定アプリがない場合)", "用 Duru 打开 .md 文件 (未设置其他默认应用时)", "Abrir archivos .md con Duru (si no hay otra app predeterminada)"), Pad, y, true);
        y = Add(page1, cbDefault) + 16;
        y = Add(page1, Label(L.T(
            "Installs for the current user only, no admin rights needed. Adds a Start menu entry and an '" + L.OpenWith + "' right-click menu.",
            "관리자 권한 없이 현재 사용자에게만 설치돼요. 우클릭 메뉴 '" + L.OpenWith + "'와 시작 메뉴도 함께 등록돼요.",
            "管理者権限なしで現在のユーザーにのみインストールされます。スタートメニューと右クリックメニュー「" + L.OpenWith + "」も追加されます。",
            "仅为当前用户安装，无需管理员权限。同时添加开始菜单和右键菜单“" + L.OpenWith + "”。",
            "Se instala solo para el usuario actual, sin permisos de administrador. Añade una entrada al menú Inicio y el menú contextual '" + L.OpenWith + "'."),
            fSmall, muted, Pad, y, TextW)) + 22;
        var btnInstall = Button(installText, true);
        btnInstall.Width = Math.Max(btnInstall.Width, TextRenderer.MeasureText(L.T("Installing…", "설치 중…", "インストール中…", "正在安装…", "Instalando…"), btnInstall.Font).Width + 32);
        var btnCancel = Button(L.T("Cancel", "취소", "キャンセル", "取消", "Cancelar"), false);
        btnCancel.Location = new Point(W - Pad - btnCancel.Width, y);
        btnInstall.Location = new Point(btnCancel.Left - 12 - btnInstall.Width, y);
        btnInstall.Click += (s, e) => DoInstall(btnInstall);
        btnCancel.Click += (s, e) => Close();
        page1.Controls.Add(btnInstall); page1.Controls.Add(btnCancel);
        int h1 = btnInstall.Bottom + 26;
        AcceptButton = btnInstall; CancelButton = btnCancel;

        // 2페이지: 완료
        page2 = new Panel { Location = Point.Empty, Visible = false };
        Header(page2, logoImg, L.T("Installation complete", "설치가 끝났어요", "インストールが完了しました", "安装完成", "Instalación completada"),
            L.T("Start Duru from the Start menu or the desktop.", "시작 메뉴나 바탕화면의 'Duru'로 실행하세요.", "スタートメニューまたはデスクトップの Duru から起動してください。", "从开始菜单或桌面启动 Duru。", "Abre Duru desde el menú Inicio o el escritorio."));
        y = 116;
        y = Add(page2, Label(L.T(
            "• Right-click a .md · .json · .yaml · .txt file → '" + L.OpenWith + "'\n• Drag files onto the window (several at once is fine)\n• Ctrl+E to edit, Ctrl+S to save",
            "• .md · .json · .yaml · .txt 파일 우클릭 → '" + L.OpenWith + "'\n• 창에 파일 끌어다 놓기 (여러 개 가능)\n• Ctrl+E 편집,  Ctrl+S 저장",
            "• .md · .json · .yaml · .txt ファイルを右クリック →「" + L.OpenWith + "」\n• ウィンドウにファイルをドロップ (複数可)\n• Ctrl+E で編集、Ctrl+S で保存",
            "• 右键单击 .md · .json · .yaml · .txt 文件 → “" + L.OpenWith + "”\n• 把文件拖到窗口中 (可一次多个)\n• Ctrl+E 编辑，Ctrl+S 保存",
            "• Clic derecho en un archivo .md · .json · .yaml · .txt → '" + L.OpenWith + "'\n• Arrastra archivos a la ventana (varios a la vez)\n• Ctrl+E para editar, Ctrl+S para guardar"),
            fBody, Color.FromArgb(60, 66, 76), Pad, y, TextW)) + 16;
        var link = new LinkLabel { Text = L.T("Make Duru the default app so files open on double-click", "더블클릭으로 바로 열리게 기본 앱으로 지정하기", "ダブルクリックで開くよう既定のアプリに設定する", "设为默认应用，双击即可打开", "Hacer de Duru la app predeterminada para abrir con doble clic"),
            Location = new Point(Pad, y), MaximumSize = new Size(TextW, 0), AutoSize = true, LinkColor = accent, Font = fBody };
        link.LinkClicked += (s, e) => OpenDefaultApps();
        y = Add(page2, link) + 18;
        cbRun = Check(L.T("Launch Duru now", "지금 Duru 실행", "今すぐ Duru を起動", "立即启动 Duru", "Abrir Duru ahora"), Pad, y, true);
        y = Add(page2, cbRun) + 26;
        var btnDone = Button(L.T("Finish", "완료", "完了", "完成", "Finalizar"), true);
        btnDone.Location = new Point(W - Pad - btnDone.Width, y);
        btnDone.Click += (s, e) =>
        {
            if (cbRun.Checked) try { Process.Start(Setup.InstalledExe); } catch { }
            Close();
        };
        page2.Controls.Add(btnDone);
        int h2 = btnDone.Bottom + 26;

        ClientSize = new Size(W, Math.Max(h1, h2));
        page1.Size = page2.Size = ClientSize;
        Controls.Add(page1); Controls.Add(page2);
    }

    void Header(Panel p, Image img, string title, string sub)
    {
        p.Controls.Add(new PictureBox { Location = new Point(Pad, 32), Size = new Size(56, 56), SizeMode = PictureBoxSizeMode.Zoom, Image = img });
        p.Controls.Add(Label(title, fTitle, ink, 104, 32));
        p.Controls.Add(Label(sub, fBody, muted, 106, 64, W - 106 - Pad));
    }

    static int Add(Panel p, Control c) { p.Controls.Add(c); return c.Bottom; }

    void DoInstall(Button b)
    {
        b.Enabled = false; b.Text = L.T("Installing…", "설치 중…", "インストール中…", "正在安装…", "Instalando…"); Cursor = Cursors.WaitCursor; Refresh();
        try
        {
            Installer.Install(cbDesktop.Checked, cbDefault.Checked);
            page1.Visible = false; page2.Visible = true;
            AcceptButton = null; CancelButton = null;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, L.T("Installation failed.", "설치하지 못했어요.", "インストールできませんでした。", "安装失败。", "No se pudo instalar.") + "\n\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            b.Enabled = true; b.Text = installText;
        }
        finally { Cursor = Cursors.Default; }
    }

    static void OpenDefaultApps()
    {
        try { Process.Start("ms-settings:defaultapps?registeredAppUser=" + Uri.EscapeDataString(Setup.AppName)); }
        catch { try { Process.Start("ms-settings:defaultapps"); } catch { } }
    }

    static Label Label(string text, Font f, Color c, int x, int y, int w = 0)
    {
        var l = new Label { Text = text, Font = f, ForeColor = c, Location = new Point(x, y), BackColor = Color.Transparent };
        if (w > 0) { l.AutoSize = false; l.Size = new Size(w, TextRenderer.MeasureText(text, f, new Size(w, 0), TextFormatFlags.WordBreak).Height + 4); }
        else l.AutoSize = true;
        return l;
    }
    CheckBox Check(string text, int x, int y, bool on)
    {
        return new CheckBox { Text = text, Location = new Point(x, y), AutoSize = true, MaximumSize = new Size(TextW, 0), Checked = on, Font = fBody, ForeColor = ink };
    }
    Button Button(string text, bool primary)
    {
        var f = new Font(L.UiFont, 9.5f, primary ? FontStyle.Bold : FontStyle.Regular);
        int w = Math.Max(96, TextRenderer.MeasureText(text, f).Width + 32);
        var b = new Button { Text = text, Size = new Size(w, 34), FlatStyle = FlatStyle.Flat, Font = f, Cursor = Cursors.Hand };
        b.FlatAppearance.BorderColor = primary ? accent : Color.FromArgb(214, 217, 222);
        b.BackColor = primary ? accent : Color.White;
        b.ForeColor = primary ? Color.White : ink;
        return b;
    }
}
