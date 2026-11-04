# Duru

**A small, fast Windows app to view and edit Markdown, JSON, YAML and text files.**

Double-click a `.md` file and read it as a clean document, with a table of contents, dark mode and code blocks you can copy. Press `Ctrl+E` to edit and `Ctrl+S` to save.

[한국어](#한국어)

## Features

- **Markdown**: tables, task lists, code blocks with a copy button, images next to the file, and links to other `.md` files that open inside Duru
- **JSON**: collapsible tree view or highlighted text, with expand/collapse all
- **YAML**: syntax colors, with top-level keys listed in the table of contents
- **Text**: shown exactly as written
- Hides front matter (`title:`, `tags:` …) at the top of a document
- Several documents in one window, with a sidebar to switch between them
- Reloads automatically when the file changes on disk
- Dark and light themes, adjustable text size, wide layout
- English, 한국어, 日本語, 简体中文, Español. Follows your Windows language, and you can change it in the ⓘ menu

## Install

1. Download `Duru-Setup.exe` from [Releases](../../releases/latest).
2. Run it. No admin rights are needed. Duru installs for your user only.
3. Right-click any `.md`, `.json`, `.yaml` or `.txt` file and choose **Open with Duru**.

> Windows may show a blue "Windows protected your PC" screen because the installer is not code-signed yet. Click **More info → Run anyway**.

**Requirements:** Windows 10 or 11 with Microsoft Edge (installed by default).

To uninstall, go to **Settings → Apps → Installed apps → Duru**.

## Shortcuts

| Key | Action |
|---|---|
| `Ctrl+O` | Open files |
| `Ctrl+E` | Switch between edit and view |
| `Ctrl+S` / `Ctrl+Shift+S` | Save / Save as |
| `Ctrl+B` / `Ctrl+I` | Bold / italic (while editing) |
| `T` | Show or hide the sidebar |
| `D` | Dark / light theme |

## Build from source

You only need Windows. The C# compiler that ships with .NET Framework 4 is enough.

```powershell
.\build.ps1
```

Output goes to `out\`: `Duru.exe`, `Duru-Setup.exe`, `Duru-Setup.zip` and `SHA256SUMS.txt`.

## Privacy

Duru works entirely on your computer. It does not send your files or any usage data anywhere. It runs a small local server on `localhost` that only Duru's own window can use.

## Third-party components

- [marked](https://github.com/markedjs/marked) (MIT)
- [Pretendard](https://github.com/orioncactus/pretendard) (SIL Open Font License 1.1)

See [THIRD_PARTY_NOTICES.txt](THIRD_PARTY_NOTICES.txt).

---

## 한국어

**Markdown · JSON · YAML · 텍스트 파일을 두루 보고 고치는 가벼운 Windows 앱이에요.**

`.md` 파일을 더블클릭하면 목차, 다크 모드, 복사 버튼이 달린 코드 블록과 함께 깔끔한 문서로 보여 줘요. `Ctrl+E`로 편집하고 `Ctrl+S`로 저장해요.

**설치**
1. [Releases](../../releases/latest)에서 `Duru-Setup.exe`를 받아요.
2. 실행하면 관리자 권한 없이 내 계정에만 설치돼요.
3. 파일을 우클릭해서 **Duru로 보기**를 누르면 돼요.

> 코드 서명이 아직 없어서 "Windows의 PC 보호" 파란 창이 뜰 수 있어요. **추가 정보 → 실행**을 누르세요.

화면 언어는 Windows 언어를 따라가고, 왼쪽 위 ⓘ 메뉴에서 바꿀 수 있어요.
