<#
.SYNOPSIS
    DLL ハイジャック対策の再検証スクリプト（Windows 専用、単一ファイル発行した RandomPasswordGenerator.exe 向け）。

.DESCRIPTION
    publish 済みの単一ファイル EXE（RandomPasswordGenerator.exe）を作業フォルダーに
    コピーし、その隣に「読み込めるが何もしない」最小の x64 DLL（おとり）を指定した名前で並べて
    起動する。起動中にプロセスへ読み込まれたモジュールを監視し、おとりが 1 つでも EXE の
    フォルダーから読み込まれたら NG。

    合格には「拒否そのものの観測」が必要（読み込みゼロだけでは通常起動と区別できない）。
    アプリは拒否メッセージの末尾に固有マーカー（RpgDllGuardRefusal）を入れる。次のどちらかで合格:
      (a) 起動時チェックの警告ダイアログ（GUI の MessageBox）に固有マーカーがあるのを動作中に観測した
      (b) 標準エラーに固有マーカーが出た（CLI、および GUI もダイアログ表示前に標準エラーへ書く）
    終了コード（1 = GeneralError）だけでは合格にしない（無関係な一般エラーと区別できないため）。

    既定の名前一覧は、このアプリ（.NET 10 + Avalonia）の対策前の実測で
    EXE のフォルダーから読み込まれた 16 個と、よく狙われる名前。
    kernel32 / advapi32 は含めていない。これらはランタイム自身（CoreLib）が起動時チェックより前に
    フルパスで読み込むため、おとりを置くとアプリ側の対策が動く前に読み込まれる
    （kernel32 は「起動途中で終了」、advapi32 は警告ダイアログは出るが読み込み済み）。
    EXE の隣に DLL を置かれる環境では防ぎようがないので、インストーラー版は Program Files に入れている。

    対策前の EXE（1.0.0）に対して実行すると NG になり、読み込まれた名前が出る。

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File Invoke-DllHijackCheck.ps1 -ExePath ..\..\_installer\bin\RandomPasswordGenerator.exe
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ExePath,

    [string[]]$Names = @(
        'bcrypt', 'ntdll', 'dwrite', 'dxgi', 'shcore', 'd3d11', 'dwmapi', 'dcomp', 'd3dcompiler_47',
        'coremessaging', 'profapi', 'cryptbase', 'sspicli', 'netapi32', 'wkscli', 'cscapi',
        'user32', 'oleaut32', 'uxtheme', 'version', 'winmm', 'iphlpapi', 'userenv', 'dbghelp'
    ),

    # 起動後に監視する秒数
    [ValidateRange(1, [int]::MaxValue)]
    [int]$Seconds = 10,

    # 起動を断ったことを表す固有マーカー。GUI / CLI の拒否メッセージ（ダイアログ・標準エラー）の末尾に必ず入る。
    # Services/WindowsDllSearchGuard.cs の RefusalMarker と一致させること。「DLL」などの一般語や終了コードだけだと、
    # 無関係な起動エラーを拒否と誤認しうるので、この固有マーカーの有無だけを拒否の根拠にする
    # 任意の文字列（特に空文字）を許すと空正規表現で何にでも一致し、通常起動を拒否成功と誤判定する。
    # サポートする固有マーカーだけを受け付ける（Services/WindowsDllSearchGuard.cs の RefusalMarker と同値）
    [ValidateSet('RpgDllGuardRefusal')]
    [string]$RefusalMarker = 'RpgDllGuardRefusal',

    # 任意。指定すると、固有マーカーに加えて「対象プロセスが自然終了し、その終了コードが
    # この値と一致する」ことも合格条件にする（CLI 経路の追加チェック。GUI はダイアログ表示中で
    # 自然終了しないので通常は指定しない）。負（既定）ならマーカーだけで判定する
    [int]$ExpectRefusalExitCode = -1
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# 対象プロセスがまだ生きていて stderr ファイルを掴んでいても読めるよう、共有読み取りで開く
function Read-SharedText([string]$path) {
    if (-not (Test-Path $path)) { return '' }
    try {
        $fs = [System.IO.File]::Open($path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
        try {
            $sr = New-Object System.IO.StreamReader($fs, [System.Text.Encoding]::UTF8)
            return $sr.ReadToEnd()
        } finally { $fs.Dispose() }
    } catch { return '' }
}

# 拒否ダイアログ（Win32 MessageBox = クラス #32770）を、対象プロセスのトップレベルウィンドウから探し、
# 見出し + Static コントロールの本文を返す。見つからなければ $null。
# 「読み込まれた DLL が無い」だけでは通常起動と区別できないので、拒否そのものを観測する
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace DllHijackCheck
{
    public static class DialogProbe
    {
        private delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc callback, IntPtr lParam);
        [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr hWnd, EnumProc callback, IntPtr lParam);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassNameW(IntPtr hWnd, StringBuilder buffer, int size);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextW(IntPtr hWnd, StringBuilder buffer, int size);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessageTimeoutW(IntPtr hWnd, uint msg, IntPtr wParam, StringBuilder lParam, uint flags, uint timeoutMs, out IntPtr result);

        private const uint WM_GETTEXT = 0x000D;
        private const uint SMTO_ABORTIFHUNG = 0x0002;

        public static string FindDialogText(int processId)
        {
            string result = null;
            EnumWindows(delegate (IntPtr hWnd, IntPtr lParam)
            {
                uint pid;
                GetWindowThreadProcessId(hWnd, out pid);
                if (pid != (uint)processId || !IsWindowVisible(hWnd)) return true;

                StringBuilder cls = new StringBuilder(256);
                GetClassNameW(hWnd, cls, cls.Capacity);
                if (cls.ToString() != "#32770") return true;

                StringBuilder caption = new StringBuilder(512);
                GetWindowTextW(hWnd, caption, caption.Capacity);
                StringBuilder text = new StringBuilder();
                text.AppendLine(caption.ToString());

                EnumChildWindows(hWnd, delegate (IntPtr child, IntPtr lParam2)
                {
                    StringBuilder childClass = new StringBuilder(256);
                    GetClassNameW(child, childClass, childClass.Capacity);
                    if (childClass.ToString() == "Static")
                    {
                        // 別プロセスのコントロールの本文は GetWindowText では取れないので WM_GETTEXT で読む。
                        // 対象が応答しないと SendMessage は無期限に止まるので、タイムアウト付き + ABORTIFHUNG を使う
                        StringBuilder body = new StringBuilder(8192);
                        IntPtr res;
                        SendMessageTimeoutW(child, WM_GETTEXT, (IntPtr)body.Capacity, body, SMTO_ABORTIFHUNG, 2000, out res);
                        if (body.Length > 0) text.AppendLine(body.ToString());
                    }
                    return true;
                }, IntPtr.Zero);

                result = text.ToString();
                return false;
            }, IntPtr.Zero);
            return result;
        }
    }
}
"@

function New-DecoyDll([string]$Path, [uint64]$ImageBase) {
    # DOS ヘッダー + PE32+ ヘッダー + .text 1 セクション。DllMain は「mov eax,1 ; ret」だけ。
    $b = New-Object byte[] 0x400
    $w16 = { param($o, $v) [BitConverter]::GetBytes([uint16]$v).CopyTo($b, $o) }
    $w32 = { param($o, $v) [BitConverter]::GetBytes([uint32]$v).CopyTo($b, $o) }
    $w64 = { param($o, $v) [BitConverter]::GetBytes([uint64]$v).CopyTo($b, $o) }
    $b[0] = 0x4D; $b[1] = 0x5A; & $w32 0x3C 0x40
    $b[0x40] = 0x50; $b[0x41] = 0x45
    $c = 0x44
    & $w16 $c 0x8664; & $w16 ($c + 2) 1; & $w16 ($c + 16) 0xF0; & $w16 ($c + 18) 0x2022
    $o = $c + 20
    & $w16 $o 0x20B; & $w32 ($o + 4) 0x200; & $w32 ($o + 16) 0x1000; & $w32 ($o + 20) 0x1000
    & $w64 ($o + 24) $ImageBase; & $w32 ($o + 32) 0x1000; & $w32 ($o + 36) 0x200
    & $w16 ($o + 40) 6; & $w16 ($o + 48) 6; & $w32 ($o + 56) 0x2000; & $w32 ($o + 60) 0x200
    & $w16 ($o + 68) 2; & $w16 ($o + 70) 0x160
    & $w64 ($o + 72) 0x100000; & $w64 ($o + 80) 0x1000; & $w64 ($o + 88) 0x100000; & $w64 ($o + 96) 0x1000
    & $w32 ($o + 108) 16
    $s = $o + 0xF0
    [System.Text.Encoding]::ASCII.GetBytes('.text').CopyTo($b, $s)
    & $w32 ($s + 8) 0x200; & $w32 ($s + 12) 0x1000; & $w32 ($s + 16) 0x200; & $w32 ($s + 20) 0x200; & $w32 ($s + 36) 0x60000020
    $b[0x200] = 0xB8; $b[0x201] = 1; $b[0x205] = 0xC3
    [System.IO.File]::WriteAllBytes($Path, $b)
}

$exe = (Resolve-Path $ExePath).Path
$work = Join-Path ([System.IO.Path]::GetTempPath()) ("dll-hijack-check-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null

# finally で確実に止められるよう、try の外で宣言しておく（$loaded.Count -gt 0 の早期 exit でも対象が残らないように）
$p = $null

try {
    Copy-Item $exe (Join-Path $work (Split-Path $exe -Leaf))
    $i = 0
    foreach ($n in $Names) {
        New-DecoyDll -Path (Join-Path $work "$n.dll") -ImageBase ([uint64]0x180000000 + [uint64]$i * 0x100000)
        $i++
    }
    Write-Host "作業フォルダー: $work"
    Write-Host "おとり DLL: $($Names.Count) 個"

    $stderrPath = Join-Path $work 'stderr.txt'
    $p = Start-Process -FilePath (Join-Path $work (Split-Path $exe -Leaf)) -WorkingDirectory $work -PassThru -RedirectStandardError $stderrPath
    # Handle に一度触れておかないと、終了後に ExitCode が取れない（PowerShell の Start-Process の癖）
    $null = $p.Handle
    $loaded = @{}
    $title = ''
    $dialogText = $null
    $deadline = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $pp = Get-Process -Id $p.Id -ErrorAction Stop
            if ($pp.MainWindowTitle) { $title = $pp.MainWindowTitle }
            if (-not $dialogText) { $dialogText = [DllHijackCheck.DialogProbe]::FindDialogText($p.Id) }
            foreach ($m in $pp.Modules) {
                if ($m.FileName.StartsWith($work, [StringComparison]::OrdinalIgnoreCase) -and $m.ModuleName -like '*.dll') {
                    $loaded[$m.ModuleName] = $true
                }
            }
        }
        catch {
            # Modules の列挙が失敗したとき、プロセスがまだ動いていれば「読み込みゼロ」と
            # 誤認してはいけない（後段で stderr 拒否だけ見て OK にしかねない）。判定不能で抜ける。
            # プロセス終了との競合（終了直後の列挙失敗）だけは通常の break に落とす
            $p.Refresh()
            if (-not $p.HasExited) {
                Write-Host ("NG（判定不能）: DLL の監視に失敗しました: {0}" -f $_.Exception.Message) -ForegroundColor Red
                exit 2
            }
            break
        }
        if ($p.HasExited) { break }
        Start-Sleep -Milliseconds 100
    }

    $p.Refresh()

    # おとりが 1 つでも EXE のフォルダーから読み込まれていたら、その時点で失格
    if ($loaded.Count -gt 0) {
        Write-Host ('NG: 次のおとりが EXE のフォルダーから読み込まれました: ' + (($loaded.Keys | Sort-Object) -join ', ')) -ForegroundColor Red
        exit 1
    }

    # 自分で止める前に「自然に終了したか」を記録する（Stop-Process 後は HasExited が true になるため）
    $exitedOnItsOwn = $p.HasExited
    $exitCode = if ($exitedOnItsOwn) { $p.ExitCode } else { $null }

    # 拒否そのものを観測できたか（読み込みゼロだけでは通常起動と区別できないので、
    # 次のいずれかの根拠が要る）:
    #   (a) 起動時チェックの警告ダイアログ（#32770）に固有マーカーが載っているのを動作中に観測した
    #   (b) 標準エラーに固有マーカーが出た（CLI、および GUI もダイアログ表示前に標準エラーへ書く）
    # 終了コード（1 = GeneralError）だけでは判定しない。無関係な一般エラーと区別できないため
    $stderr = Read-SharedText $stderrPath
    if (-not $dialogText -and -not $exitedOnItsOwn) { $dialogText = [DllHijackCheck.DialogProbe]::FindDialogText($p.Id) }

    $markerPattern   = [regex]::Escape($RefusalMarker)
    $refusedByDialog = [bool]($dialogText -and $dialogText -match $markerPattern)
    $refusedByStderr = [bool]($stderr -match $markerPattern)

    if ($exitedOnItsOwn) {
        Write-Host ("プロセスは終了しました: 終了コード 0x{0:X8}" -f $exitCode)
    }
    else {
        Write-Host "プロセスは動作中（ウィンドウ: '$title'）。停止します。"
        Stop-Process -Id $p.Id -Force
    }
    if ($dialogText) { Write-Host '拒否ダイアログ:'; Write-Host $dialogText }
    if ($stderr)     { Write-Host '標準エラー出力:'; Write-Host $stderr }

    $refusedByMarker = [bool]($refusedByDialog -or $refusedByStderr)
    $exitCodeOk = ($ExpectRefusalExitCode -lt 0) -or ($exitedOnItsOwn -and $exitCode -eq $ExpectRefusalExitCode)
    if ($refusedByMarker -and $exitCodeOk) {
        Write-Host 'OK: 起動時チェックが拒否し、EXE のフォルダーからは何も読み込まれませんでした。' -ForegroundColor Green
        exit 0
    }
    if ($refusedByMarker -and -not $exitCodeOk) {
        Write-Host ("NG: 拒否マーカーは出ましたが、終了コードが期待値 {0} と一致しません（自然終了=$exitedOnItsOwn 実際={1}）。" -f $ExpectRefusalExitCode, $exitCode) -ForegroundColor Red
        exit 1
    }

    if ($exitedOnItsOwn -and $exitCode -eq 0) {
        Write-Host 'NG: 終了コード 0 で終わりました。起動時チェックの拒否が観測できていません（通常起動した可能性があります）。' -ForegroundColor Red
        exit 1
    }
    if ($exitedOnItsOwn) {
        Write-Host 'NG（判定不能）: 拒否を確認できないまま終了しました。おとりのどれかが読み込まれて起動途中で落ちた可能性があります。' -ForegroundColor Red
        exit 2
    }
    Write-Host 'NG: 動作中でしたが、起動時チェックの拒否（ダイアログ・標準エラー・終了コード）を確認できませんでした（通常起動した可能性があります）。' -ForegroundColor Red
    exit 1
}

finally {
    # 途中で抜けても（早期 exit を含む）、対象プロセスが動いていれば止めて、
    # 実際に終了する（＝EXE のロックが外れる）のを待ってから片付ける
    if ($p) {
        try {
            if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue }
            $null = $p.WaitForExit(5000)
        } catch { }
    }
    # プロセス終了後も Windows が EXE のハンドルを手放すまで一瞬ラグがある。
    # 数回だけ待って再試行し、それでも消せなければ警告する（握りつぶさない）
    $removed = $false
    for ($i = 0; $i -lt 10 -and -not $removed; $i++) {
        try { Remove-Item -Recurse -Force $work -ErrorAction Stop; $removed = $true }
        catch { Start-Sleep -Milliseconds 200 }
    }
    if (-not $removed -and (Test-Path $work)) {
        Write-Warning ("作業フォルダーを削除できませんでした（EXE が掴まれたままの可能性）: {0}" -f $work)
    }
}
