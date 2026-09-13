namespace PasswordStrength;

/// <summary>
/// パスワード強度評価の入口。
/// 「分かっているものは計算し、分からないものだけ推定する」:
/// アプリが生成したものは生成条件から正確に、ユーザー入力・編集済みのものだけ zxcvbn で推定する。
/// </summary>
public interface IPasswordStrengthService
{
    /// <summary>アプリが生成したランダム文字列パスワードを、生成条件から正確に評価する。</summary>
    PasswordStrengthResult EvaluateGeneratedPassword(GeneratedPasswordParameters parameters);

    /// <summary>アプリが辞書から生成したパスフレーズを、生成条件から正確に評価する。</summary>
    PasswordStrengthResult EvaluateGeneratedPassphrase(PassphraseParameters parameters);

    /// <summary>ユーザーが自由入力した、または生成後に編集したパスワードを推定評価する。</summary>
    PasswordStrengthResult EvaluateUserPassword(string password, IEnumerable<string>? userInputs = null);
}
