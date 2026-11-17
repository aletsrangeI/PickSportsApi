using Domain.Entities;
using Interface.UseCases.Knockout;

namespace UseCases.Liguilla;

public class KnockoutStrategyResolver : IKnockoutStrategyResolver
{
    private readonly Dictionary<string, IKnockoutFormatStrategy> _strategies;

    public KnockoutStrategyResolver(IEnumerable<IKnockoutFormatStrategy> strategies)
    {
        _strategies = new Dictionary<string, IKnockoutFormatStrategy>(StringComparer.OrdinalIgnoreCase);
        foreach (var strategy in strategies)
        {
            _strategies[strategy.FormatCode] = strategy;
        }
    }

    public IKnockoutFormatStrategy? For(League league) => ByCode(league.KnockoutFormatCode);

    public IKnockoutFormatStrategy? ByCode(string? formatCode)
    {
        if (string.IsNullOrWhiteSpace(formatCode))
            return null;

        return _strategies.TryGetValue(formatCode.Trim(), out var strategy) ? strategy : null;
    }
}
