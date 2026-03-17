using Sundex.Markup.Document;

namespace Sundex.Markup.Abstract;

public interface IComponentBuilder
{
    public SundexComponent CreateComponent(SundexDocument layout, ISundexContext context);
}