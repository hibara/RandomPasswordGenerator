namespace PasswordStrength;

/// <summary>強度評価がどのように求められたか。</summary>
public enum PasswordStrengthSource
{
    /// <summary>
    /// アプリ自身が生成したパスワード。生成条件（文字集合サイズ・長さ・辞書サイズ・語数）から
    /// エントロピーを正確に計算したもの。
    /// </summary>
    GeneratedExact,

    /// <summary>
    /// ユーザーが自由入力した、または生成後に編集したパスワード。
    /// 生成過程が分からないため zxcvbn による推定値。
    /// </summary>
    Estimated,
}
