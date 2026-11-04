using System;
using System.Globalization;

// Windows 표시 언어에 맞춰 문구를 고른다. 지원: 영어(기본)·한국어·일본어·중국어·스페인어
static class L
{
    public static readonly string Code = Pick();

    static string Pick()
    {
        string c = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return Array.IndexOf(new[] { "ko", "ja", "zh", "es" }, c) >= 0 ? c : "en";
    }

    public static string T(string en, string ko, string ja, string zh, string es)
    {
        switch (Code)
        {
            case "ko": return ko;
            case "ja": return ja;
            case "zh": return zh;
            case "es": return es;
            default: return en;
        }
    }

    // 설치 창 글꼴
    public static string UiFont { get { return T("Segoe UI", "Malgun Gothic", "Yu Gothic UI", "Microsoft YaHei UI", "Segoe UI"); } }

    public static string Tagline { get { return T("View and edit Markdown, JSON, YAML and text", "Markdown · JSON · YAML · 텍스트를 두루 보고 고치는 에디터", "Markdown · JSON · YAML · テキストを表示・編集", "查看和编辑 Markdown · JSON · YAML · 文本", "Visor y editor de Markdown, JSON, YAML y texto"); } }
    public static string OpenWith { get { return T("Open with Duru", "Duru로 보기", "Duru で開く", "用 Duru 打开", "Abrir con Duru"); } }
    public static string TextFiles { get { return T("Text", "텍스트", "テキスト", "文本", "Texto"); } }
    public static string AllFiles { get { return T("All files", "모든 파일", "すべてのファイル", "所有文件", "Todos los archivos"); } }
}
