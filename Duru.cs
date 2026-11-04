using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

// 구조: 첫 실행이 localhost 에 작은 서버를 띄우고 Edge 앱 창으로 뷰어를 연다.
// 서버가 파일 읽기/저장/열기·저장 대화상자를 맡는다. 두 번째 실행부터는 서버에 파일만 넘기고 창을 앞으로 가져온다.
// 뷰어 창이 닫히면 서버도 종료된다.
static class Duru
{
    const int BasePort = 48231;
    const string TitleSuffix = "Duru";
    static readonly string AppDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Duru");
    static readonly string PortFile = Path.Combine(AppDir, "port.txt");
    static readonly string TokenFile = Path.Combine(AppDir, "token.txt");
    static readonly JavaScriptSerializer Js = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
    static readonly Dictionary<string, byte[]> Static = new Dictionary<string, byte[]>();
    static readonly List<object> Inbox = new List<object>();
    static string Token;
    static int Port;

    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr p);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder sb, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder sb, int n);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, IntPtr pid);
    [DllImport("user32.dll")] static extern bool AttachThreadInput(uint a, uint b, bool attach);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    delegate bool EnumProc(IntPtr h, IntPtr p);

    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--uninstall") { Uninstall(); return; }
        bool created;
        using (var mtx = new Mutex(true, @"Local\DuruServer", out created))
        {
            try
            {
                if (created) Server(args); else Client(args);
            }
            catch (Exception e) { MessageBox.Show(e.Message, L.T("Duru error", "Duru 오류", "Duru エラー", "Duru 错误", "Error de Duru")); }
            finally { if (created) mtx.ReleaseMutex(); }
        }
    }

    // ── 제거 (설정 > 앱 에서 호출) ──
    static void Uninstall()
    {
        if (MessageBox.Show(L.T("Uninstall Duru?\n\nYour document files will not be deleted.", "Duru를 제거할까요?\n\n작성한 문서 파일은 지워지지 않아요.", "Duru をアンインストールしますか？\n\nドキュメントファイルは削除されません。", "要卸载 Duru 吗？\n\n你的文档文件不会被删除。", "¿Desinstalar Duru?\n\nTus archivos de documentos no se eliminarán."), L.T("Uninstall Duru", "Duru 제거", "Duru のアンインストール", "卸载 Duru", "Desinstalar Duru"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        string me = Assembly.GetExecutingAssembly().Location;
        IntPtr h = FindViewer();
        if (h != IntPtr.Zero) PostMessage(h, 0x0010, IntPtr.Zero, IntPtr.Zero);   // 열린 뷰어 창 닫기
        foreach (var p in Process.GetProcesses())
        {
            try { if (p.Id != Process.GetCurrentProcess().Id && string.Equals(p.MainModule.FileName, me, StringComparison.OrdinalIgnoreCase)) p.Kill(); } catch { }
        }
        Setup.Unregister();
        try { File.Delete(PortFile); File.Delete(TokenFile); } catch { }
        // 실행 중인 자기 자신은 바로 못 지우므로, 끝난 뒤 설치 폴더 삭제
        string dir = Path.GetDirectoryName(me);
        if (string.Equals(dir, Setup.InstallDir, StringComparison.OrdinalIgnoreCase))
            Process.Start(new ProcessStartInfo("cmd.exe", "/c ping -n 3 127.0.0.1 >nul & rmdir /s /q \"" + dir + "\"") { CreateNoWindow = true, UseShellExecute = false });
        MessageBox.Show(L.T("Duru has been uninstalled.", "Duru를 제거했어요.", "Duru をアンインストールしました。", "Duru 已卸载。", "Duru se ha desinstalado."), "Duru");
    }
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, int msg, IntPtr w, IntPtr l);

    // ── 두 번째 실행: 서버에 파일 넘기고 창 앞으로 ──
    static void Client(string[] args)
    {
        var paths = new List<string>();
        foreach (var a in args) if (File.Exists(a)) paths.Add(Path.GetFullPath(a));
        for (int i = 0; i < 50; i++)
        {
            try
            {
                int port = int.Parse(File.ReadAllText(PortFile).Trim());
                string token = File.ReadAllText(TokenFile).Trim();
                var req = (HttpWebRequest)WebRequest.Create("http://localhost:" + port + "/api/push");
                req.Method = "POST"; req.ContentType = "application/json"; req.Headers["X-Token"] = token; req.Timeout = 3000;
                byte[] body = Encoding.UTF8.GetBytes(Js.Serialize(new { paths }));
                using (var s = req.GetRequestStream()) s.Write(body, 0, body.Length);
                using (req.GetResponse()) { }
                IntPtr h = FindViewer();
                if (h != IntPtr.Zero) Focus(h); else Launch(port);
                return;
            }
            catch { Thread.Sleep(100); }
        }
        throw new Exception(L.T("Could not connect to the running Duru. Please try again in a moment.", "실행 중인 Duru에 연결하지 못했어요. 잠시 후 다시 열어주세요.", "実行中の Duru に接続できませんでした。少し待ってからもう一度開いてください。", "无法连接到正在运行的 Duru。请稍后再试。", "No se pudo conectar con Duru en ejecución. Inténtalo de nuevo en un momento."));
    }

    // ── 첫 실행: 서버 + 창 ──
    static void Server(string[] args)
    {
        Directory.CreateDirectory(AppDir);
        try { string old = Path.Combine(AppDir, "app"); if (Directory.Exists(old)) Directory.Delete(old, true); } catch { }
        Token = Guid.NewGuid().ToString("N");
        var asm = Assembly.GetExecutingAssembly();
        foreach (var name in new[] { "viewer.html", "marked.min.js", "PretendardVariable.woff2", "icon.png", "app.ico", "notices.txt" })
            using (var s = asm.GetManifestResourceStream(name))
            using (var m = new MemoryStream()) { s.CopyTo(m); Static[name] = m.ToArray(); }
        Static["viewer.html"] = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(Static["viewer.html"]).Replace("__TOKEN__", Token).Replace("__VERSION__", Setup.Version).Replace("__LANG__", L.Code).Replace("__ANSI__", AnsiName()));

        HttpListener l = null;
        for (Port = BasePort; Port < BasePort + 30; Port++)
        {
            try { l = new HttpListener(); l.Prefixes.Add("http://localhost:" + Port + "/"); l.Start(); break; }
            catch { l = null; }
        }
        if (l == null) throw new Exception(L.T("Could not open a local port for the viewer.", "뷰어용 로컬 포트를 열지 못했어요.", "ビューア用のローカルポートを開けませんでした。", "无法为查看器打开本地端口。", "No se pudo abrir un puerto local para el visor."));
        File.WriteAllText(PortFile, Port.ToString());
        File.WriteAllText(TokenFile, Token);
        foreach (var a in args) if (File.Exists(a)) Enqueue(Path.GetFullPath(a));

        new Thread(() =>
        {
            while (l.IsListening)
            {
                HttpListenerContext c;
                try { c = l.GetContext(); } catch { break; }
                ThreadPool.QueueUserWorkItem(_ => Handle(c));
            }
        }) { IsBackground = true }.Start();

        Launch(Port);
        // 뷰어 창이 사라지면 종료
        var start = DateTime.Now; int miss = 0; IntPtr tagged = IntPtr.Zero;
        for (int i = 0; i < 40 && tagged == IntPtr.Zero; i++) { Thread.Sleep(150); IntPtr h0 = FindViewer(); if (h0 != IntPtr.Zero) { TagWindow(h0); tagged = h0; } }
        while (true)
        {
            Thread.Sleep(1500);
            IntPtr vw = FindViewer();
            if (vw != IntPtr.Zero) { miss = 0; if (vw != tagged) { TagWindow(vw); tagged = vw; } }
            else if ((DateTime.Now - start).TotalSeconds > 20 && ++miss >= 3) break;
        }
        try { File.Delete(PortFile); File.Delete(TokenFile); } catch { }
        l.Stop();
    }

    // UTF-8 이 아닌 옛 파일을 읽을 때 쓸 시스템 기본 인코딩 (한국 euc-kr, 일본 shift_jis 등)
    static string AnsiName()
    {
        try { return Encoding.GetEncoding(System.Globalization.CultureInfo.CurrentCulture.TextInfo.ANSICodePage).WebName; }
        catch { return "windows-1252"; }
    }

    // Edge 창을 작업 표시줄에서 Duru 로 보이게 (선명한 Duru.ico 사용)
    static void TagWindow(IntPtr h)
    {
        string exe = File.Exists(Setup.InstalledExe) ? Setup.InstalledExe : Assembly.GetExecutingAssembly().Location;
        string icon = (File.Exists(Setup.IconFile) ? Setup.IconFile : exe) + ",0";
        TaskbarId.ApplyToWindow(h, Setup.AppId, exe, icon, "Duru");
    }

    static void Enqueue(string p) { var e = Entry(p); lock (Inbox) Inbox.Add(e); }

    static string Mtime(string p) { return File.GetLastWriteTimeUtc(p).Ticks.ToString(); }

    static Dictionary<string, object> Entry(string p)
    {
        return new Dictionary<string, object> {
            { "path", p }, { "name", Path.GetFileName(p) }, { "mtime", Mtime(p) },
            { "b64", Convert.ToBase64String(File.ReadAllBytes(p)) }
        };
    }

    static void Handle(HttpListenerContext c)
    {
        var req = c.Request; var res = c.Response;
        try
        {
            if (req.Url.Host != "localhost") { res.StatusCode = 403; return; }
            string path = req.Url.AbsolutePath;
            // 문서 안 상대경로 이미지: /local/{token}/{인코딩된 절대경로} (이미지 확장자만)
            if (req.RawUrl.StartsWith("/local/"))
            {
                var parts = req.RawUrl.Split('?')[0].Substring(7).Split(new[] { '/' }, 2);
                if (parts.Length < 2 || parts[0] != Token) { res.StatusCode = 403; return; }
                string fp = Uri.UnescapeDataString(parts[1]);
                string ext = Path.GetExtension(fp).ToLowerInvariant();
                var types = new Dictionary<string, string> { { ".png", "image/png" }, { ".jpg", "image/jpeg" }, { ".jpeg", "image/jpeg" }, { ".gif", "image/gif" }, { ".webp", "image/webp" }, { ".svg", "image/svg+xml" }, { ".bmp", "image/bmp" } };
                if (!types.ContainsKey(ext) || !File.Exists(fp)) { res.StatusCode = 404; return; }
                byte[] img = File.ReadAllBytes(fp);
                res.ContentType = types[ext];
                res.OutputStream.Write(img, 0, img.Length);
                return;
            }
            if (!path.StartsWith("/api/"))
            {
                string name = path == "/" ? "viewer.html" : path.TrimStart('/');
                byte[] data;
                if (!Static.TryGetValue(name, out data)) { res.StatusCode = 404; return; }
                res.ContentType = name.EndsWith(".html") ? "text/html; charset=utf-8" : name.EndsWith(".js") ? "text/javascript; charset=utf-8"
                                : name.EndsWith(".png") ? "image/png" : name.EndsWith(".ico") ? "image/x-icon" : name.EndsWith(".txt") ? "text/plain; charset=utf-8" : "font/woff2";
                res.Headers["Cache-Control"] = "no-store";
                res.OutputStream.Write(data, 0, data.Length);
                return;
            }
            if (req.Headers["X-Token"] != Token) { res.StatusCode = 403; return; }
            string bodyText;
            using (var r = new StreamReader(req.InputStream, Encoding.UTF8)) bodyText = r.ReadToEnd();
            var q = bodyText.Length > 0 ? Js.Deserialize<Dictionary<string, object>>(bodyText) : new Dictionary<string, object>();
            object result = null;
            switch (path)
            {
                case "/api/inbox":
                    lock (Inbox) { result = Inbox.ToArray(); Inbox.Clear(); }
                    break;
                case "/api/push":
                    foreach (var p in (ArrayList)q["paths"]) Enqueue((string)p);
                    result = true;
                    break;
                case "/api/stat":
                    { string p = (string)q["path"]; result = File.Exists(p) ? Mtime(p) : null; }
                    break;
                case "/api/read":
                    { string p = (string)q["path"]; result = File.Exists(p) ? Entry(p) : null; }
                    break;
                case "/api/save":
                    { string p = (string)q["path"]; WriteText(p, (string)q["text"]); result = new Dictionary<string, object> { { "mtime", Mtime(p) } }; }
                    break;
                case "/api/saveas":
                    {
                        string p = AskSave((string)q["name"], q.ContainsKey("dir") ? q["dir"] as string : null);
                        if (p != null)
                        {
                            WriteText(p, (string)q["text"]);
                            result = new Dictionary<string, object> { { "path", p }, { "name", Path.GetFileName(p) }, { "mtime", Mtime(p) } };
                        }
                    }
                    break;
                case "/api/locate":
                    {
                        long size = q.ContainsKey("size") && q["size"] != null ? Convert.ToInt64(q["size"]) : -1;
                        long ms = q.ContainsKey("mtime") && q["mtime"] != null ? Convert.ToInt64(q["mtime"]) : -1;
                        var list = new List<object>();
                        foreach (var p in Locate((string)q["name"], size, ms)) list.Add(Entry(p));
                        result = list;
                    }
                    break;
                case "/api/open":
                    {
                        var list = new List<object>();
                        foreach (var p in AskOpen()) list.Add(Entry(p));
                        result = list;
                    }
                    break;
                default: res.StatusCode = 404; return;
            }
            byte[] json = Encoding.UTF8.GetBytes(Js.Serialize(result));
            res.ContentType = "application/json; charset=utf-8";
            res.OutputStream.Write(json, 0, json.Length);
        }
        catch (Exception e)
        {
            try { res.StatusCode = 500; byte[] b = Encoding.UTF8.GetBytes(e.Message); res.OutputStream.Write(b, 0, b.Length); } catch { }
        }
        finally { try { res.Close(); } catch { } }
    }

    // 원래 파일의 BOM·줄끝(CRLF) 방식을 유지해서 UTF-8 로 저장
    static void WriteText(string p, string text)
    {
        bool bom = false, crlf = Environment.NewLine == "\r\n";
        if (File.Exists(p))
        {
            byte[] old = File.ReadAllBytes(p);
            bom = old.Length >= 3 && old[0] == 0xEF && old[1] == 0xBB && old[2] == 0xBF;
            crlf = Encoding.UTF8.GetString(old).Contains("\r\n") || old.Length == 0;
        }
        text = text.Replace("\r\n", "\n");
        if (crlf) text = text.Replace("\n", "\r\n");
        File.WriteAllText(p, text, new UTF8Encoding(bom));
    }

    // ── 끌어다 놓은 파일의 원래 위치 찾기 ──
    // 브라우저는 경로를 알려주지 않으므로, 열려 있는 탐색기 창 폴더·최근 항목·바탕화면·다운로드·문서에서
    // 같은 이름 + 같은 크기 + 같은 수정시각인 파일을 찾는다.
    static List<string> Locate(string name, long size, long ms)
    {
        var dirs = new List<string>();
        var found = new List<string>();
        Sta(() =>
        {
            try
            {
                dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application"));
                foreach (dynamic w in shell.Windows())
                {
                    try { string p = w.Document.Folder.Self.Path; if (!string.IsNullOrEmpty(p)) dirs.Add(p); } catch { }
                }
            }
            catch { }
            try
            {
                string lnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Recent), name + ".lnk");
                if (File.Exists(lnk))
                {
                    dynamic ws = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                    string t = ws.CreateShortcut(lnk).TargetPath;
                    if (!string.IsNullOrEmpty(t)) dirs.Insert(0, Path.GetDirectoryName(t));
                }
            }
            catch { }
            return 0;
        });
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        dirs.Add(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
        dirs.Add(Path.Combine(home, "Downloads"));
        dirs.Add(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        foreach (var d in dirs)
        {
            try
            {
                string f = Path.Combine(d, name);
                if (!File.Exists(f) || found.Exists(x => string.Equals(x, f, StringComparison.OrdinalIgnoreCase))) continue;
                var fi = new FileInfo(f);
                if (size >= 0 && fi.Length != size) continue;
                if (ms >= 0 && Math.Abs((fi.LastWriteTimeUtc - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds - ms) > 2000) continue;
                found.Add(f);
            }
            catch { }
        }
        return found;
    }

    // ── 파일 대화상자 (뷰어 창 위에 뜨도록) ──
    static T Sta<T>(Func<T> f)
    {
        T r = default(T); Exception ex = null;
        var t = new Thread(() => { try { r = f(); } catch (Exception e) { ex = e; } });
        t.SetApartmentState(ApartmentState.STA); t.Start(); t.Join();
        if (ex != null) throw ex;
        return r;
    }
    static Form Owner()
    {
        var f = new Form { TopMost = true, ShowInTaskbar = false, FormBorderStyle = FormBorderStyle.None, Size = new Size(1, 1), StartPosition = FormStartPosition.Manual, Opacity = 0 };
        RECT r; IntPtr v = FindViewer();
        if (v != IntPtr.Zero && GetWindowRect(v, out r)) f.Location = new Point((r.L + r.R) / 2 - 300, (r.T + r.B) / 2 - 220);
        f.Show();
        IntPtr fg = GetForegroundWindow();
        uint fgThread = GetWindowThreadProcessId(fg, IntPtr.Zero), me = GetCurrentThreadId();
        AttachThreadInput(me, fgThread, true); SetForegroundWindow(f.Handle); f.Activate(); AttachThreadInput(me, fgThread, false);
        return f;
    }
    static string AskSave(string name, string dir)
    {
        string ext = Path.GetExtension(name ?? "").ToLowerInvariant().TrimStart('.');
        var filters = new Dictionary<string, string> {
            { "json", "JSON (*.json)|*.json" }, { "yaml", "YAML (*.yaml;*.yml)|*.yaml;*.yml" }, { "yml", "YAML (*.yaml;*.yml)|*.yaml;*.yml" },
            { "txt", L.TextFiles + " (*.txt)|*.txt" } };
        if (!filters.ContainsKey(ext)) ext = "md";
        string filter = (ext == "md" ? "Markdown (*.md)|*.md" : filters[ext]) + "|" + L.AllFiles + " (*.*)|*.*";
        return Sta(() =>
        {
            using (var o = Owner())
            using (var d = new SaveFileDialog { Title = L.T("Save as", "다른 이름으로 저장", "名前を付けて保存", "另存为", "Guardar como"), FileName = name,
                Filter = filter, DefaultExt = ext, AddExtension = true, OverwritePrompt = true })
            {
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) d.InitialDirectory = dir;
                return d.ShowDialog(o) == DialogResult.OK ? d.FileName : null;
            }
        });
    }
    static string[] AskOpen()
    {
        return Sta(() =>
        {
            using (var o = Owner())
            using (var d = new OpenFileDialog { Title = L.T("Open file", "파일 열기", "ファイルを開く", "打开文件", "Abrir archivo"), Multiselect = true, Filter = L.T("Documents", "문서", "ドキュメント", "文档", "Documentos") + " (*.md;*.json;*.yaml;*.yml;*.txt)|*.md;*.markdown;*.json;*.yaml;*.yml;*.txt|Markdown (*.md;*.markdown)|*.md;*.markdown|JSON (*.json)|*.json|YAML (*.yaml;*.yml)|*.yaml;*.yml|" + L.TextFiles + " (*.txt)|*.txt|" + L.AllFiles + " (*.*)|*.*" })
                return d.ShowDialog(o) == DialogResult.OK ? d.FileNames : new string[0];
        });
    }

    // ── 창 찾기/띄우기 ──
    static IntPtr FindViewer()
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows((h, p) =>
        {
            if (!IsWindowVisible(h)) return true;
            var cls = new StringBuilder(64); GetClassName(h, cls, 64);
            if (cls.ToString() != "Chrome_WidgetWin_1") return true;
            var t = new StringBuilder(512); GetWindowText(h, t, 512);
            if (t.ToString().EndsWith(TitleSuffix)) { found = h; return false; }
            return true;
        }, IntPtr.Zero);
        return found;
    }
    static void Focus(IntPtr h)
    {
        if (IsIconic(h)) ShowWindow(h, 9);
        SetForegroundWindow(h);
    }
    static void Launch(int port)
    {
        string edge = null;
        foreach (var root in new[] { Environment.GetEnvironmentVariable("ProgramFiles(x86)"), Environment.GetEnvironmentVariable("ProgramFiles") })
        {
            if (root == null) continue;
            string c = Path.Combine(root, @"Microsoft\Edge\Application\msedge.exe");
            if (File.Exists(c)) { edge = c; break; }
        }
        if (edge == null) throw new Exception(L.T("Microsoft Edge was not found. Duru needs Edge to show its window.", "Microsoft Edge를 찾을 수 없어요. Duru는 Edge로 창을 띄워요.", "Microsoft Edge が見つかりません。Duru は Edge でウィンドウを表示します。", "找不到 Microsoft Edge。Duru 需要 Edge 来显示窗口。", "No se encontró Microsoft Edge. Duru necesita Edge para mostrar su ventana."));
        Process.Start(edge, "--app=http://localhost:" + port + "/ --window-size=1280,900");
    }
}
