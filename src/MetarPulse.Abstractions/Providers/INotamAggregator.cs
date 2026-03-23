namespace MetarPulse.Abstractions.Providers;

/// <summary>
/// Birden fazla INotamProvider'ı yöneten toplayıcı arayüz.
/// NotamController ve NotamPollingService bu arayüzü inject eder.
/// </summary>
public interface INotamAggregator : INotamProvider
{
    IReadOnlyList<INotamProvider> GetAllProviders();
}
