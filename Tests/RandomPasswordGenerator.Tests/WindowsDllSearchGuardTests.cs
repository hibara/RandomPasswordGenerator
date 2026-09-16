using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using RandomPasswordGenerator.Services;

namespace RandomPasswordGenerator.Tests;

/// <summary>Windows 専用のテスト。ほかの OS ではスキップする。</summary>
public sealed class WindowsOnlyFactAttribute : FactAttribute
{
    public WindowsOnlyFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = "Windows only";
        }
    }
}

/// <summary>
/// Windows で、テストホストの EXE がテスト出力フォルダーにあるときだけ実行する。
/// テストホストは条件によって共有の dotnet.exe や NuGet キャッシュの testhost.exe になることがあり、
/// その隣にはおとりを書き込めない（書き込めても共有フォルダーを汚す）ので、そのときはスキップする。
/// </summary>
public sealed class WindowsTestHostInOutputDirectoryFactAttribute : FactAttribute
{
    public WindowsTestHostInOutputDirectoryFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = "Windows only";
            return;
        }

        var processDirectory = Path.GetDirectoryName(Environment.ProcessPath);
        var outputDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(AppContext.BaseDirectory));
        if (processDirectory is null
            || !string.Equals(Path.GetFullPath(processDirectory), outputDirectory, StringComparison.OrdinalIgnoreCase))
        {
            Skip = "The test host is not running from the test output directory";
        }
    }
}

/// <summary>
/// DLL ハイジャック対策（<see cref="WindowsDllSearchGuard"/>）の検証。
/// <para>
/// SetDefaultDllDirectories はプロセス全体に効いて元に戻せないため、このテストはテストホスト全体の
/// 検索順序を System32 に絞る。ほかのテストは managed のみか、ネイティブをフルパスで読むので影響しない。
/// </para>
/// </summary>
[SupportedOSPlatform("windows")]
public class WindowsDllSearchGuardTests
{
    private const int ErrorModNotFound = 126;   // ERROR_MOD_NOT_FOUND
    private const int ErrorBadExeFormat = 193;  // ERROR_BAD_EXE_FORMAT

    private static readonly string System32 =
        Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.System)).TrimEnd(Path.DirectorySeparatorChar);

    [Fact]
    public void Windows以外では何もせず成功を返し起動も止めない()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        Assert.True(WindowsDllSearchGuard.Install());
        Assert.Empty(WindowsDllSearchGuard.FindForeignDlls());
    }

    [WindowsOnlyFact]
    public void Installは成功を返し冪等で既定のDLL検索パスも制限される()
    {
        Assert.True(WindowsDllSearchGuard.Install());
        Assert.True(WindowsDllSearchGuard.Install());

        Assert.True(WindowsDllSearchGuard.IsInstalled);
        Assert.True(WindowsDllSearchGuard.DefaultDllDirectoriesRestricted);
    }

    [WindowsOnlyFact]
    public void 対策後はカレントディレクトリとアプリケーションディレクトリのDLLが探されない()
    {
        // おとりは PE でない中身にしてあるので、検索順序に含まれていれば ERROR_BAD_EXE_FORMAT (193)、
        // 含まれていなければ ERROR_MOD_NOT_FOUND (126) になり、「読まれたか／探されもしなかったか」を区別できる
        var name = "rpg_decoy_" + Guid.NewGuid().ToString("N") + ".dll";
        var decoyDirectories = new[]
        {
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar),
        };
        var decoys = new List<string>();
        try
        {
            foreach (var directory in decoyDirectories)
            {
                var path = Path.Combine(directory, name);
                if (decoys.Contains(path))
                {
                    continue;
                }

                File.WriteAllText(path, "not a PE file");
                decoys.Add(path);
            }

            // 前提の確認: おとり自体はフルパスなら「見つかるが PE でない」(193) になる
            Assert.Equal(ErrorBadExeFormat, TryLoad(decoys[0]));

            Assert.True(WindowsDllSearchGuard.Install());

            // 名前だけの LoadLibrary で、おとりが探されもしないこと
            Assert.Equal(ErrorModNotFound, TryLoad(name));
        }
        finally
        {
            foreach (var path in decoys)
            {
                try { File.Delete(path); } catch { /* 後始末の失敗は無視 */ }
            }
        }
    }

    [WindowsOnlyFact]
    public void システムDLLはSystem32から解決される()
    {
        WindowsDllSearchGuard.Install();

        // 拡張子あり／なし、KnownDLLs に含まれるもの／含まれないもの
        foreach (var name in new[] { "dwmapi", "dwmapi.dll", "shcore", "bcrypt.dll", "ntdll.dll" })
        {
            var handle = WindowsDllSearchGuard.Resolve(name, typeof(object).Assembly, null);

            Assert.NotEqual(IntPtr.Zero, handle);
            AssertLoadedFromSystem32(handle);
        }
    }

    [WindowsOnlyFact]
    public void 同梱のネイティブDLLはアプリ側のフォルダーから解決される()
    {
        WindowsDllSearchGuard.Install();

        var handle = WindowsDllSearchGuard.Resolve("libSkiaSharp", typeof(object).Assembly, null);

        Assert.NotEqual(IntPtr.Zero, handle);
        var path = ModulePath(handle);
        Assert.StartsWith(AppContext.BaseDirectory, path, StringComparison.OrdinalIgnoreCase);
        Assert.False(path.StartsWith(System32, StringComparison.OrdinalIgnoreCase));
    }

    [WindowsOnlyFact]
    public void リゾルバーは見つからない名前を拒否しパス付きは既定に委ねる()
    {
        WindowsDllSearchGuard.Install();
        var assembly = typeof(object).Assembly;

        // System32 にも同梱フォルダーにも無い名前 → 既定解決へ戻さず拒否（DllNotFoundException）。
        // 既定解決は単一ファイル発行で EXE のフォルダーも探すため、そこへ落とさない
        var name = "rpg_missing_" + Guid.NewGuid().ToString("N") + ".dll";
        Assert.Throws<DllNotFoundException>(() => WindowsDllSearchGuard.Resolve(name, assembly, null));

        // パス付き・ルート付きは呼び出し元の意図どおり（触らない）
        Assert.Equal(IntPtr.Zero, WindowsDllSearchGuard.Resolve(@"sub\bcrypt.dll", assembly, null));
        Assert.Equal(IntPtr.Zero, WindowsDllSearchGuard.Resolve("sub/bcrypt.dll", assembly, null));
        Assert.Equal(IntPtr.Zero, WindowsDllSearchGuard.Resolve(@"C:\nowhere\bcrypt.dll", assembly, null));
    }

    [WindowsTestHostInOutputDirectoryFact]
    public void ネイティブの検索フラグなしLoadLibraryでもEXEのフォルダーの偽DLLは読み込まれない()
    {
        // テストホストの EXE と同じフォルダー（属性で出力フォルダーと一致することを確認済み）に、
        // System32 にもある名前の偽 DLL を置く
        var appDirectory = Path.GetFullPath(Path.GetDirectoryName(Environment.ProcessPath)!);
        Assert.True(File.Exists(Path.Combine(System32, "wkscli.dll")));
        var decoy = Path.Combine(appDirectory, "wkscli.dll");
        File.WriteAllBytes(decoy, MinimalDll());

        try
        {
            WindowsDllSearchGuard.Install();

            var handle = LoadLibraryW("wkscli.dll");

            Assert.NotEqual(IntPtr.Zero, handle);
            AssertLoadedFromSystem32(handle);
        }
        finally
        {
            File.Delete(decoy);
        }
    }

    [WindowsOnlyFact]
    public void 名前指定のPInvokeでもEXEのフォルダーの偽DLLは読み込まれない()
    {
        // ランタイムが最初に探すフォルダー（テストでは出力フォルダー）に、System32 にもある名前の偽 DLL を置く
        Assert.True(File.Exists(Path.Combine(System32, "version.dll")));
        var decoy = Path.Combine(AppContext.BaseDirectory, "version.dll");
        File.WriteAllBytes(decoy, MinimalDll());

        try
        {
            WindowsDllSearchGuard.Install();

            // 偽 DLL が束縛されると EntryPointNotFoundException になる
            var size = GetFileVersionInfoSizeW(Path.Combine(System32, "kernel32.dll"), out _);

            Assert.True(size > 0);
            Assert.DoesNotContain(
                Process.GetCurrentProcess().Modules.Cast<ProcessModule>(),
                m => string.Equals(m.FileName, decoy, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            File.Delete(decoy);
        }
    }

    [WindowsOnlyFact]
    public void Install後はbcryptなど暗号系DLLがSystem32から固定読み込みされている()
    {
        // CoreLib の P/Invoke（RandomNumberGenerator → bcrypt など）はリゾルバーを通らないため、
        // Install が System32 の本物を先に読み込んで固定していることを、モジュールの実パスで確かめる。
        // 固定済みなら、あとで EXE の隣へ同名 DLL を置いても名前一致で既読モジュールが再利用される
        Assert.True(WindowsDllSearchGuard.Install());

        foreach (var dll in new[] { "bcrypt.dll", "ncrypt.dll", "crypt32.dll", "advapi32.dll" })
        {
            var handle = GetModuleHandleW(dll);
            Assert.NotEqual(IntPtr.Zero, handle); // Install で読み込まれている
            AssertLoadedFromSystem32(handle);
        }

        // 実際に乱数が引けること（bcrypt が壊れていない）も確認
        var buffer = new byte[32];
        RandomNumberGenerator.Fill(buffer);
        Assert.Contains(buffer, b => b != 0);
    }

    [WindowsOnlyFact]
    public void FindDllsはフォルダー直下のdllだけを大文字小文字を区別せず返す()
    {
        var directory = Path.Combine(Path.GetTempPath(), "rpg-dllcheck-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(directory, "sub"));
        Directory.CreateDirectory(Path.Combine(directory, "dir.dll")); // フォルダーは対象外
        foreach (var name in new[] { "b.dll", "A.DLL", "readme.txt", "x.dllx", "dll", "readme.dll.txt", Path.Combine("sub", "c.dll") })
        {
            File.WriteAllText(Path.Combine(directory, name), "");
        }

        try
        {
            Assert.True(WindowsDllSearchGuard.TryFindDlls(directory, out var found));
            Assert.Equal(["A.DLL", "b.dll"], found);
            Assert.Equal(["A.DLL", "b.dll"], WindowsDllSearchGuard.FindDlls(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [WindowsOnlyFact]
    public void 空のフォルダーはTryFindDllsが成功して空を返す()
    {
        var directory = Path.Combine(Path.GetTempPath(), "rpg-emptydir-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            Assert.True(WindowsDllSearchGuard.TryFindDlls(directory, out var found));
            Assert.Empty(found);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [WindowsOnlyFact]
    public void 存在しないフォルダーはTryFindDllsが失敗を返しFindDllsは空を返す()
    {
        var directory = Path.Combine(Path.GetTempPath(), "rpg-nodir-" + Guid.NewGuid().ToString("N"));

        // 列挙できない = 安全と断定できない → TryFindDlls は false（呼び出し側は起動を断る）
        Assert.False(WindowsDllSearchGuard.TryFindDlls(directory, out var dlls));
        Assert.Empty(dlls);
        // 診断用の薄いラッパーは従来どおり空を返す
        Assert.Empty(WindowsDllSearchGuard.FindDlls(directory));
    }

    [Fact]
    public void テストホストは単一ファイル発行ではないので起動時チェックは何も返さない()
    {
        // 出力フォルダーには DLL が並ぶが、単一ファイル発行でなければ起動を止めない
        Assert.False(WindowsDllSearchGuard.IsSingleFileBundle);
        Assert.Empty(WindowsDllSearchGuard.FindForeignDlls());
    }

    [Fact]
    public void 起動を断るメッセージにはフォルダーとDLL名と固有マーカーが入る()
    {
        var message = WindowsDllSearchGuard.BuildRefusalMessage(["bcrypt.dll", "dwmapi.dll"]);

        Assert.Contains(AppContext.BaseDirectory, message);
        Assert.Contains("bcrypt.dll", message);
        Assert.Contains("dwmapi.dll", message);
        Assert.EndsWith(WindowsDllSearchGuard.RefusalMarker, message);
    }

    [Fact]
    public void 検索順序を限定できなかったときのメッセージにも固有マーカーが入る()
    {
        var message = WindowsDllSearchGuard.BuildInstallFailureMessage();

        Assert.Contains("DLL", message);
        Assert.EndsWith(WindowsDllSearchGuard.RefusalMarker, message);
    }

    /// <summary>LoadLibrary を試し、失敗なら Win32 エラーコード、成功なら 0 を返す。</summary>
    private static int TryLoad(string nameOrPath)
    {
        var handle = LoadLibraryW(nameOrPath);
        var error = Marshal.GetLastWin32Error();
        if (handle != IntPtr.Zero)
        {
            FreeLibrary(handle);
            return 0;
        }

        return error;
    }

    /// <summary>読み込んだモジュールの親フォルダーが「まさに System32」であることを完全一致で確かめる
    /// （StartsWith だと C:\Windows\System32-evil のような接頭辞一致も通ってしまう）。</summary>
    private static void AssertLoadedFromSystem32(IntPtr handle)
    {
        var parent = Path.GetFullPath(Path.GetDirectoryName(ModulePath(handle))!).TrimEnd(Path.DirectorySeparatorChar);
        Assert.Equal(System32, parent, ignoreCase: true);
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadLibraryW(string lpLibFileName);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FreeLibrary(IntPtr hModule);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string lpModuleName);

    [DllImport("version.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern uint GetFileVersionInfoSizeW(string lptstrFilename, out uint lpdwHandle);

    private static string ModulePath(IntPtr handle)
    {
        foreach (ProcessModule module in Process.GetCurrentProcess().Modules)
        {
            if (module.BaseAddress == handle)
            {
                return module.FileName;
            }
        }

        throw new InvalidOperationException($"モジュール 0x{handle:X} がプロセスにありません");
    }

    /// <summary>
    /// 読み込めるが何もしない最小の x64 DLL（DllMain が TRUE を返すだけ。インポート／エクスポートなし）。
    /// </summary>
    private static byte[] MinimalDll()
    {
        var b = new byte[0x400];
        void W16(int o, ushort v) => BitConverter.GetBytes(v).CopyTo(b, o);
        void W32(int o, uint v) => BitConverter.GetBytes(v).CopyTo(b, o);
        void W64(int o, ulong v) => BitConverter.GetBytes(v).CopyTo(b, o);

        b[0] = (byte)'M'; b[1] = (byte)'Z'; W32(0x3C, 0x40);           // DOS ヘッダー
        b[0x40] = (byte)'P'; b[0x41] = (byte)'E';                       // PE シグネチャ
        const int coff = 0x44;
        W16(coff, 0x8664); W16(coff + 2, 1); W16(coff + 16, 0xF0); W16(coff + 18, 0x2022); // x64, 1 セクション, DLL
        const int opt = coff + 20;
        W16(opt, 0x20B); W32(opt + 4, 0x200); W32(opt + 16, 0x1000); W32(opt + 20, 0x1000);
        W64(opt + 24, 0x1_8000_0000); W32(opt + 32, 0x1000); W32(opt + 36, 0x200);
        W16(opt + 40, 6); W16(opt + 48, 6); W32(opt + 56, 0x2000); W32(opt + 60, 0x200);
        W16(opt + 68, 2); W16(opt + 70, 0x160);                        // GUI, DYNAMIC_BASE | NX | HIGH_ENTROPY
        W64(opt + 72, 0x100000); W64(opt + 80, 0x1000); W64(opt + 88, 0x100000); W64(opt + 96, 0x1000);
        W32(opt + 108, 16);                                             // データディレクトリ数（すべて 0）
        const int sec = opt + 0xF0;
        ".text"u8.CopyTo(b.AsSpan(sec));
        W32(sec + 8, 0x200); W32(sec + 12, 0x1000); W32(sec + 16, 0x200); W32(sec + 20, 0x200); W32(sec + 36, 0x60000020);
        b[0x200] = 0xB8; b[0x201] = 1; b[0x205] = 0xC3;                 // mov eax,1 ; ret
        return b;
    }
}
