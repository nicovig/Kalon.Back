namespace Kalon.Back.Exceptions;

public class InsufficientSearchCreditsException : Exception
{
    public decimal Available { get; }
    public decimal Required { get; }

    public InsufficientSearchCreditsException(decimal available, decimal required)
        : base($"Crédits insuffisants : {available} disponibles, {required} requis.")
    {
        Available = available;
        Required = required;
    }
}
