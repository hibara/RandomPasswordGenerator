// Windows での DLL ハイジャック対策。
// _doc/dll-hijack-hardening/WindowsDllSearchGuard.cs（AttacheCase 5.5 で確定させた汎用テンプレート）を
// このアプリ向けに 3 か所（namespace / ProductName / RefusalMarker）だけ書き換えたもの。
// Program.Main の先頭で Install() → FindForeignDlls() の順に呼び、どちらかで止まったら起動しない（fail closed）。
// 検証は Tests/DllHijackCheck/Invoke-DllHijackCheck.ps1（-RefusalMarker の既定値を RefusalMarker と一致させている）。
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace RandomPasswordGenerator.Services;

/// <summary>
/// Windows での DLL ハイジャック（DLL 検索順序の悪用）対策。3 段構え:
/// <list type="number">
/// <item>A. <c>SetDllDirectory("")</c> + <c>SetDefaultDllDirectories(SYSTEM32 | USER_DIRS)</c> で、
/// ネイティブコードの名前検索から exe のフォルダー・カレント・PATH を外す。</item>
/// <item>B. 全アセンブリに <see cref="NativeLibrary.SetDllImportResolver"/> を登録し、P/Invoke 対象は
/// System32 だけから読む。同梱ネイティブは展開先のフルパスで読み、解決できないパス無し名は拒否する。</item>
/// <item>C. 単一ファイル発行のとき、exe と同じフォルダーに .dll があれば起動を断る
/// （<see cref="FindForeignDlls"/>）。列挙は自前の FindFirstFileW で行い、検査自体が偽 DLL を踏まない。</item>
/// </list>
/// ランタイム本体（CoreLib）の P/Invoke（bcrypt/ntdll/kernel32/advapi32）にはリゾルバーが呼ばれず
/// 防げない。kernel32/advapi32 は C の検査より前にランタイムが読むので、そこは配置場所（Program Files）で守る。
/// Windows 以外では何もしない。
/// </summary>
public static class WindowsDllSearchGuard
{
    /// <summary>警告ダイアログのタイトルに使うアプリ名（リソースを介さず固定文字列にする）。</summary>
    private const string ProductName = "Random Password Generator";

    /// <summary>
    /// 起動を断ったことを表す固有の識別子。拒否メッセージ（ダイアログ・標準エラー）の末尾に必ず入れ、
    /// 検証スクリプトがこの文字列で「確かに拒否した」と判定できるようにする。「DLL」などの一般語や
    /// 終了コードだけだと、無関係な起動エラーを拒否と誤認しうるため。Invoke-DllHijackCheck.ps1 の
    /// -RefusalMarker の既定値とこの値を一致させること。
    /// </summary>
    public const string RefusalMarker = "RpgDllGuardRefusal";

    private static readonly object Gate = new();
    private static string[] _bundledNativeDirectories = [];
    private static bool _resolverRegistered;

    /// <summary>
    /// CoreLib が P/Invoke で（リゾルバーを通さず）読む暗号系システム DLL。<see cref="Install"/> で
    /// System32 の本物を先に読み込んでプロセスに固定し、起動時チェック後に同名 DLL を置かれても
    /// 拾われないようにする。bcrypt = RandomNumberGenerator / AES-GCM(CNG)、ncrypt = CNG 鍵、
    /// crypt32 / advapi32 = DPAPI（ProtectedData）。アプリが使う暗号 API に合わせて増減してよい。
    /// </summary>
    private static readonly string[] CoreLibSecurityDlls =
        ["bcrypt.dll", "ncrypt.dll", "crypt32.dll", "advapi32.dll"];

    /// <summary><see cref="Install"/> が【成功して】完了しているか（テスト・診断用）。失敗時は true にならない。</summary>
    public static bool IsInstalled { get; private set; }

    /// <summary>SetDefaultDllDirectories の設定に成功したか（テスト・診断用）。</summary>
    public static bool DefaultDllDirectoriesRestricted { get; private set; }

    /// <summary>
    /// 単一ファイル発行（PublishSingleFile）として動いているか。バンドルされたアセンブリは Location が空になる。
    /// 開発時のビルドやテストホストでは空にならないので、起動時チェックは自動的に無効になる。
    /// </summary>
    public static bool IsSingleFileBundle
    {
        get
        {
#pragma warning disable IL3000 // 単一ファイルでは Location が空になることを利用して判定している
            return string.IsNullOrEmpty(Assembly.GetEntryAssembly()?.Location);
#pragma warning restore IL3000
        }
    }

    /// <summary>
    /// 対策 A・B を有効にする。Main の先頭、他のライブラリを呼ぶ前に呼ぶ。複数回呼んでも安全。
    /// Windows 以外では何もせず true。検索順序の設定に失敗したときは false を返す
    /// （呼び出し側は起動を中止すること。黙って続けると対策前と同じ状態で動くことになる）。
    /// </summary>
    public static bool Install()
    {
        if (!OperatingSystem.IsWindows()) return true;
        return InstallOnWindows();
    }

    [SupportedOSPlatform("windows")]
    private static bool InstallOnWindows()
    {
        lock (Gate)
        {
            if (IsInstalled) return true;

            // リゾルバーの登録は 1 回だけ（失敗後の呼び直しで AssemblyLoad の購読が重複しないように）
            if (!_resolverRegistered)
            {
                _bundledNativeDirectories = GetBundledNativeDirectories();

                // リゾルバーが使う LoadLibraryExW を先に束縛しておく（リゾルバーの中から初めて呼ぶと
                // 束縛のためにリゾルバーが再入して無限再帰になる）
                NativeMethods.LoadLibraryExW("kernel32.dll", IntPtr.Zero, NativeMethods.LOAD_LIBRARY_SEARCH_SYSTEM32);

                // CoreLib の P/Invoke はリゾルバーを通らず、SetDefaultDllDirectories でも縛れない。とくに
                // RandomNumberGenerator は [LibraryImport("bcrypt.dll")]（System32 未指定）で bcrypt を「遅延」で
                // 読むため、起動時チェック後に exe の隣へ bcrypt.dll を置かれ、その後はじめて乱数を引くと偽物を
                // 拾いうる。暗号系システム DLL を System32 の本物として先に読み込み、プロセスに固定する
                // （以後は名前一致で既読モジュールが再利用され、あとから置かれた同名 DLL は読まれない）
                // 固定に失敗した DLL は穴が残るので fail closed で起動を断る（_resolverRegistered をまだ立てない
                // ので呼び直せば最初からやり直す。System32 にこれらは必ずあるので失敗は事実上起きない）
                foreach (var systemDll in CoreLibSecurityDlls)
                {
                    var preloaded = NativeMethods.LoadLibraryExW(systemDll, IntPtr.Zero, NativeMethods.LOAD_LIBRARY_SEARCH_SYSTEM32);
                    if (preloaded == IntPtr.Zero)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"Failed to preload {systemDll} from System32: {Marshal.GetLastWin32Error()}");
                        DefaultDllDirectoriesRestricted = false;
                        IsInstalled = false;
                        return false;
                    }
                }

                AppDomain.CurrentDomain.AssemblyLoad += (_, e) => RegisterResolver(e.LoadedAssembly);
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    RegisterResolver(assembly);
                }

                _resolverRegistered = true;
            }

            // 2 本の API は独立に呼ぶ。GetLastWin32Error は各呼び出しの直後に取る
            var isDllDirectorySet = NativeMethods.SetDllDirectoryW("");
            var dllDirectoryError = Marshal.GetLastWin32Error();
            var isDefaultDirectoriesSet = NativeMethods.SetDefaultDllDirectories(
                NativeMethods.LOAD_LIBRARY_SEARCH_SYSTEM32 | NativeMethods.LOAD_LIBRARY_SEARCH_USER_DIRS);
            var defaultDirectoriesError = Marshal.GetLastWin32Error();

            if (!isDllDirectorySet)
            {
                System.Diagnostics.Debug.WriteLine("SetDllDirectory(\"\") failed: " + dllDirectoryError);
            }

            if (!isDefaultDirectoriesSet)
            {
                System.Diagnostics.Debug.WriteLine("SetDefaultDllDirectories failed: " + defaultDirectoriesError);
            }

            // 両方成功したときだけ「完了」。失敗なら呼び出し側が起動を中止する
            DefaultDllDirectoriesRestricted = isDllDirectorySet && isDefaultDirectoriesSet;
            IsInstalled = DefaultDllDirectoriesRestricted;
            return IsInstalled;
        }
    }

    /// <summary>「安全と断定できない」ことを表す番目印。フォルダーを列挙できなかったときに
    /// この 1 件を返し、呼び出し側の Count &gt; 0 判定で起動を断らせる（fail closed）。</summary>
    internal const string EnumerationFailedMarker = "(Could not list the DLLs in the program folder)";

    /// <summary>
    /// 対策 C。単一ファイル発行で動いているとき、exe と同じフォルダー（直下のみ）にある .dll の
    /// ファイル名を返す。空なら起動してよい。開発時のビルドや Windows 以外では常に空。列挙自体に
    /// 失敗したときは <see cref="EnumerationFailedMarker"/> を 1 件返し、呼び出し側に起動を断らせる（fail closed）。
    /// 列挙には Directory.EnumerateFiles（内部で ntdll.dll を P/Invoke する）ではなく自前の FindFirstFileW を使う。
    /// </summary>
    public static IReadOnlyList<string> FindForeignDlls()
    {
        if (!OperatingSystem.IsWindows() || !IsSingleFileBundle)
        {
            return [];
        }

        // 列挙に失敗したら「DLL が無い」ではなく「安全と断定できない」として扱い、起動を断る
        if (!TryFindDlls(AppContext.BaseDirectory, out var dlls))
        {
            return [EnumerationFailedMarker];
        }

        return dlls;
    }

    /// <summary>起動を断るときの利用者向けメッセージ（末尾に <see cref="RefusalMarker"/> が入る）。
    /// 表示言語の設定はまだ読めない段階なので、OS の UI 言語が日本語なら日本語、それ以外は英語。</summary>
    public static string BuildRefusalMessage(IReadOnlyList<string> dlls)
    {
        var body = string.Format(
            CultureInfo.InvariantCulture,
            IsJapaneseUi() ? MessageJa : MessageEn,
            AppContext.BaseDirectory,
            string.Join(Environment.NewLine, dlls));
        return body + Environment.NewLine + Environment.NewLine + RefusalMarker;
    }

    /// <summary><see cref="Install"/> が false を返したとき（検索順序を限定できなかった）の
    /// 起動中止メッセージ（末尾に <see cref="RefusalMarker"/> が入る）。</summary>
    public static string BuildInstallFailureMessage()
    {
        var body = IsJapaneseUi() ? InstallFailureJa : InstallFailureEn;
        return body + Environment.NewLine + Environment.NewLine + RefusalMarker;
    }

    /// <summary>GUI 用: 起動を断る警告ダイアログを出す（GUI 初期化前なので Win32 の MessageBox を直接使う）。</summary>
    public static void ShowRefusalDialog(IReadOnlyList<string> dlls)
    {
        if (!OperatingSystem.IsWindows()) return;
        var message = BuildRefusalMessage(dlls);
        EchoToStandardError(message);
        ShowDialogOnWindows(message);
    }

    /// <summary>GUI 用: 検索順序を限定できなかったときの警告ダイアログ。</summary>
    public static void ShowInstallFailureDialog()
    {
        if (!OperatingSystem.IsWindows()) return;
        var message = BuildInstallFailureMessage();
        EchoToStandardError(message);
        ShowDialogOnWindows(message);
    }

    // GUI アプリでも、標準エラーがつながっているとき（スクリプト・CI からの起動）は拒否理由を書き出す。
    // 通常のダブルクリック起動ではコンソールが無く、書き込みは無害に捨てられる
    private static void EchoToStandardError(string message)
    {
        try { Console.Error.WriteLine(message); }
        catch { /* コンソールが無い・閉じている場合は無視 */ }
    }

    [SupportedOSPlatform("windows")]
    private static void ShowDialogOnWindows(string message)
    {
        NativeMethods.MessageBoxW(
            IntPtr.Zero, message, ProductName,
            NativeMethods.MB_OK | NativeMethods.MB_ICONERROR | NativeMethods.MB_SETFOREGROUND);
    }

    // 起動時チェックの前に文字列ハッシュ（Marvin → bcrypt.dll）を使わないよう、辞書やリソースマネージャーを
    // 介さず定数で持つ（{0}=フォルダー, {1}=見つかった DLL の一覧）
    private const string MessageJa =
        "プログラムと同じフォルダーに DLL ファイルがあるため、起動を中止しました。\n\n" +
        "フォルダー: {0}\n\n{1}\n\n" +
        "このプログラムは実行ファイルの隣に DLL ファイルを必要としません。想定外の DLL は、Windows の" +
        "システムファイルの代わりに読み込まれるおそれがあります（DLL ハイジャック）。" +
        ProductName + " を DLL のない専用フォルダーに移してから、もう一度起動してください。";

    private const string MessageEn =
        "DLL files were found in the same folder as the program, so it did not start.\n\n" +
        "Folder: {0}\n\n{1}\n\n" +
        "This program needs no DLL files next to its executable. An unexpected DLL there could be loaded " +
        "in place of a Windows system file (DLL hijacking). Move " + ProductName +
        " to its own folder that contains no DLL files, then start it again.";

    private const string InstallFailureJa =
        "DLL の読み込み先を Windows のシステムフォルダーに限定する保護（DLL ハイジャック対策）を" +
        "有効にできなかったため、起動を中止しました。\n\n" +
        "このプログラムは Windows 10 以降で動作します。もう一度起動しても同じ場合は、開発元へお知らせください。";

    private const string InstallFailureEn =
        ProductName + " could not enable the protection that restricts DLL loading to the Windows system folder " +
        "(DLL hijacking protection), so it did not start.\n\n" +
        "This program requires Windows 10 or later. If this happens again, please report it to the developer.";

    private static bool IsJapaneseUi() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ja", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// フォルダー直下の .dll ファイル名を列挙する。成功時だけ true を返し <paramref name="dlls"/> に入れる。
    /// FindFirstFileW / FindNextFileW がアクセス拒否などで失敗したときは false（呼び出し側は「安全と断定
    /// できない」として扱う）。フォルダーが実在して空（ERROR_FILE_NOT_FOUND）のときは true + 空。
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal static bool TryFindDlls(string directory, out IReadOnlyList<string> dlls)
    {
        const int ErrorFileNotFound = 2;   // ERROR_FILE_NOT_FOUND（一致するものが無い）
        const int ErrorNoMoreFiles = 18;   // ERROR_NO_MORE_FILES（列挙が正常に終わった）

        var found = new List<string>();
        dlls = found;

        var handle = NativeMethods.FindFirstFileW(Path.Combine(directory, "*"), out var data);
        if (handle == NativeMethods.INVALID_HANDLE_VALUE)
        {
            // 「一致するものが無い」だけは成功（空）扱い。それ以外（アクセス拒否・パス無し等）は失敗
            return Marshal.GetLastWin32Error() == ErrorFileNotFound;
        }

        try
        {
            do
            {
                if ((data.dwFileAttributes & NativeMethods.FILE_ATTRIBUTE_DIRECTORY) == 0
                    && data.cFileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    found.Add(data.cFileName);
                }
            }
            while (NativeMethods.FindNextFileW(handle, out data));

            // FindNextFileW が false で抜けたとき、正常終了でなければ列挙は途中で失敗している
            if (Marshal.GetLastWin32Error() != ErrorNoMoreFiles)
            {
                return false;
            }
        }
        finally
        {
            NativeMethods.FindClose(handle);
        }

        found.Sort(StringComparer.OrdinalIgnoreCase);
        return true;
    }

    /// <summary>フォルダー直下の .dll ファイル名を返す（列挙失敗時は空）。診断・テスト用。</summary>
    [SupportedOSPlatform("windows")]
    internal static IReadOnlyList<string> FindDlls(string directory)
        => TryFindDlls(directory, out var dlls) ? dlls : [];

    [SupportedOSPlatform("windows")]
    private static void RegisterResolver(Assembly assembly)
    {
        try
        {
            NativeLibrary.SetDllImportResolver(assembly, Resolve);
        }
        catch (InvalidOperationException)
        {
            // そのライブラリ自身がリゾルバーを登録済み。ライブラリの判断に任せる
        }
        catch (ArgumentException)
        {
            // 動的アセンブリなど、リゾルバーを登録できないもの
        }
    }

    /// <summary>
    /// P/Invoke の DLL 解決。System32 にある DLL はそこから、同梱ネイティブ DLL は展開先から読む。
    /// パス（区切り・ルート）付きの名前は呼び出し元の意図どおり <see cref="IntPtr.Zero"/> を返して既定解決へ。
    /// パス無しの名前がどちらにも無いときは、既定解決へ戻さず <see cref="DllNotFoundException"/> を投げて拒否する
    /// （既定解決は単一ファイル発行で exe のフォルダーも探すため、そこへ落とさない）。
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        // パス付きで指定されたものは呼び出し元の意図どおりにする
        if (libraryName.AsSpan().IndexOfAny('\\', '/') >= 0 || Path.IsPathRooted(libraryName))
        {
            return IntPtr.Zero;
        }

        // 1) System32 のみ（拡張子がなければローダーが .dll を補う）
        var handle = NativeMethods.LoadLibraryExW(libraryName, IntPtr.Zero, NativeMethods.LOAD_LIBRARY_SEARCH_SYSTEM32);
        if (handle != IntPtr.Zero)
        {
            return handle;
        }

        // 2) 同梱ネイティブ DLL をフルパスで読む。依存 DLL は System32 だけから解決し、
        //    同梱 DLL 同士の依存が必要なときのために「その DLL のフォルダー + System32」でもう一度だけ試す
        var fileName = libraryName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? libraryName : libraryName + ".dll";
        foreach (var directory in _bundledNativeDirectories)
        {
            var path = Path.Combine(directory, fileName);
            if (!File.Exists(path))
            {
                continue;
            }

            handle = NativeMethods.LoadLibraryExW(path, IntPtr.Zero, NativeMethods.LOAD_LIBRARY_SEARCH_SYSTEM32);
            if (handle == IntPtr.Zero)
            {
                handle = NativeMethods.LoadLibraryExW(
                    path, IntPtr.Zero,
                    NativeMethods.LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | NativeMethods.LOAD_LIBRARY_SEARCH_SYSTEM32);
            }

            if (handle != IntPtr.Zero)
            {
                return handle;
            }
        }

        // パス無しの名前が System32 にも同梱先にも無い。既定解決へ戻すと単一ファイル発行では exe の
        // フォルダーも探され、起動時チェックを通ったあとに置かれた DLL を拾いうるので、拒否する
        throw new DllNotFoundException(
            $"Native library '{libraryName}' was not found in System32 or the bundled native directory.");
    }

    /// <summary>
    /// ランタイムがネイティブ DLL を探すフォルダー。単一ファイル発行では %TEMP% の展開先（と exe の
    /// フォルダー）、通常ビルドでは出力フォルダーがホストから渡される。単一ファイル発行では exe の
    /// フォルダーだけを除外する（残すと、起動時チェックを通ったあとに exe の隣へ置かれた DLL を
    /// Resolve() の同梱先フルパス経路で拾い、チェックを迂回されるため。同梱ネイティブは展開先から読める）。
    /// </summary>
    private static string[] GetBundledNativeDirectories()
    {
        var directories = new List<string>();
        var baseDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(AppContext.BaseDirectory));

        if (AppContext.GetData("NATIVE_DLL_SEARCH_DIRECTORIES") is string configured)
        {
            foreach (var directory in configured.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!Path.IsPathRooted(directory))
                {
                    continue;
                }

                var normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
                if (IsSingleFileBundle && string.Equals(normalized, baseDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                directories.Add(normalized);
            }
        }

        // 展開先が渡されない環境（通常ビルド）だけ、出力フォルダーを既定にする。
        // 単一ファイル発行では exe のフォルダーを既定に足さない（上と同じ迂回を避ける）
        if (directories.Count == 0 && !IsSingleFileBundle)
        {
            directories.Add(baseDirectory);
        }

        return [.. directories];
    }

    [SupportedOSPlatform("windows")]
    private static class NativeMethods
    {
        public const uint LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR = 0x00000100;
        public const uint LOAD_LIBRARY_SEARCH_USER_DIRS = 0x00000400;
        public const uint LOAD_LIBRARY_SEARCH_SYSTEM32 = 0x00000800;

        public const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
        public static readonly IntPtr INVALID_HANDLE_VALUE = new(-1);

        public const uint MB_OK = 0x00000000;
        public const uint MB_ICONERROR = 0x00000010;
        public const uint MB_SETFOREGROUND = 0x00010000;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WIN32_FIND_DATAW
        {
            public uint dwFileAttributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
            public uint nFileSizeHigh;
            public uint nFileSizeLow;
            public uint dwReserved0;
            public uint dwReserved1;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string cFileName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
            public string cAlternateFileName;
        }

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr LoadLibraryExW(string lpLibFileName, IntPtr hFile, uint dwFlags);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetDllDirectoryW(string lpPathName);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetDefaultDllDirectories(uint directoryFlags);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr FindFirstFileW(string lpFileName, out WIN32_FIND_DATAW lpFindFileData);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FindNextFileW(IntPtr hFindFile, out WIN32_FIND_DATAW lpFindFileData);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FindClose(IntPtr hFindFile);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        public static extern int MessageBoxW(IntPtr hWnd, string lpText, string lpCaption, uint uType);
    }
}
