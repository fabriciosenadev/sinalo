namespace Sinalo.Application.Appearance;

public enum ThemePreference
{
    System = 0,
    Light = 1,
    Dark = 2
}

public interface IThemePreferenceService
{
    Task<ThemePreference> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(ThemePreference preference, CancellationToken cancellationToken = default);
}
