using Domain.Entities;

namespace Interface.UseCases.Knockout;

public interface IKnockoutStrategyResolver
{
    /// <summary>Estrategia de la liga segun <c>KnockoutFormatCode</c>; <c>null</c> si no tiene o es desconocida. Nunca lanza.</summary>
    IKnockoutFormatStrategy? For(League league);

    /// <summary>Estrategia por codigo; <c>null</c> si es desconocido.</summary>
    IKnockoutFormatStrategy? ByCode(string? formatCode);
}
