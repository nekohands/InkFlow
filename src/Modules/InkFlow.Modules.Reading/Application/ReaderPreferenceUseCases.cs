using InkFlow.Modules.Reading.Domain;

namespace InkFlow.Modules.Reading.Application;

/// <summary>Reader 偏好读写用例，独立于书架、历史和正文进度。</summary>
internal sealed class ReaderPreferenceUseCases(
    IReadingStateRepository repository,
    TimeProvider clock)
{
    public async Task<ReaderPreferenceView> GetAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        ValidateUser(userId);
        var preference = await repository
            .GetPreferencesAsync(userId, cancellationToken)
            .ConfigureAwait(false)
            ?? ReaderPreference.CreateDefault(userId, clock.GetUtcNow());
        return ToView(preference);
    }

    public async Task<ReadingOperationResult<ReaderPreferenceView>> UpdateAsync(
        Guid userId,
        int? fontSizePercent,
        int? lineHeightPercent,
        ReaderTheme? theme,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty ||
            (fontSizePercent is null && lineHeightPercent is null && theme is null))
        {
            return ReadingOperationResult<ReaderPreferenceView>.Failure(
                ReadingResultStatus.InvalidRequest);
        }

        var preference = await repository
            .GetPreferencesAsync(userId, cancellationToken)
            .ConfigureAwait(false)
            ?? ReaderPreference.CreateDefault(userId, clock.GetUtcNow());

        var nextFontSize = fontSizePercent ?? preference.FontSizePercent;
        var nextLineHeight = lineHeightPercent ?? preference.LineHeightPercent;
        var nextTheme = theme ?? preference.Theme;
        try
        {
            preference.Update(nextFontSize, nextLineHeight, nextTheme, clock.GetUtcNow());
        }
        catch (ArgumentOutOfRangeException)
        {
            return ReadingOperationResult<ReaderPreferenceView>.Failure(
                ReadingResultStatus.InvalidRequest);
        }

        await repository.UpsertPreferencesAsync(preference, cancellationToken)
            .ConfigureAwait(false);
        return ReadingOperationResult<ReaderPreferenceView>.Ok(ToView(preference));
    }

    private static ReaderPreferenceView ToView(ReaderPreference preference) =>
        new(
            preference.FontSizePercent,
            preference.LineHeightPercent,
            preference.Theme.ToString(),
            preference.UpdatedAt);

    private static void ValidateUser(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("user id must not be empty.", nameof(userId));
        }
    }
}
