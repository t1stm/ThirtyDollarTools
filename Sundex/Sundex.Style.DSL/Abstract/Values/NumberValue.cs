using System.Globalization;

namespace Sundex.Style.DSL.Abstract.Values;

public record NumberValue(float Value, string Unit) : IStyleValue
{
    object IStyleValue.Value => Value;

    public override string ToString()
    {
        return string.Create(CultureInfo.InvariantCulture, $"{Value}{Unit}");
    }
}