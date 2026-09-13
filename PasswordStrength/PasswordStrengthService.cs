namespace PasswordStrength;

/// <summary>
/// <see cref="IPasswordStrengthService"/> の既定実装。
/// DI コンテナ無しでも <c>new PasswordStrengthService()</c> でそのまま使える。
/// </summary>
public sealed class PasswordStrengthService : IPasswordStrengthService
{
    private readonly IPasswordEstimator _estimator;
    private readonly PasswordStrengthScoreMapper _scoreMapper;
    private readonly GeneratedPasswordEntropyCalculator _passwordEntropy;
    private readonly PassphraseEntropyCalculator _passphraseEntropy;

    /// <param name="estimator">自由入力の推定器。省略時は <see cref="ZxcvbnPasswordEstimator"/>。</param>
    /// <param name="scoreMapper">Score 変換ポリシー。省略時は zxcvbn と同じ境界。</param>
    public PasswordStrengthService(
        IPasswordEstimator? estimator = null,
        PasswordStrengthScoreMapper? scoreMapper = null)
    {
        _estimator = estimator ?? new ZxcvbnPasswordEstimator();
        _scoreMapper = scoreMapper ?? new PasswordStrengthScoreMapper();
        _passwordEntropy = new GeneratedPasswordEntropyCalculator();
        _passphraseEntropy = new PassphraseEntropyCalculator();
    }

    /// <inheritdoc />
    public PasswordStrengthResult EvaluateGeneratedPassword(GeneratedPasswordParameters parameters) =>
        FromExactEntropy(_passwordEntropy.Calculate(parameters));

    /// <inheritdoc />
    public PasswordStrengthResult EvaluateGeneratedPassphrase(PassphraseParameters parameters) =>
        FromExactEntropy(_passphraseEntropy.Calculate(parameters));

    /// <inheritdoc />
    public PasswordStrengthResult EvaluateUserPassword(string password, IEnumerable<string>? userInputs = null)
    {
        ArgumentNullException.ThrowIfNull(password);

        var estimate = _estimator.Estimate(password, userInputs);
        return new PasswordStrengthResult
        {
            Source = PasswordStrengthSource.Estimated,
            EstimatedGuessBits = PasswordStrengthScoreMapper.GuessesLog10ToBits(estimate.GuessesLog10),
            GuessesLog10 = estimate.GuessesLog10,
            Score = _scoreMapper.ScoreFromGuessesLog10(estimate.GuessesLog10),
            Warning = estimate.Warning,
            Suggestions = estimate.Suggestions,
        };
    }

    private PasswordStrengthResult FromExactEntropy(double bits)
    {
        var guessesLog10 = PasswordStrengthScoreMapper.EntropyBitsToGuessesLog10(bits);
        return new PasswordStrengthResult
        {
            Source = PasswordStrengthSource.GeneratedExact,
            ExactEntropyBits = bits,
            GuessesLog10 = guessesLog10,
            Score = _scoreMapper.ScoreFromGuessesLog10(guessesLog10),
        };
    }
}
