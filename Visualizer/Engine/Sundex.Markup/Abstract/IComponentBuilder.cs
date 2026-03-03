using Sunder.Markup.Document;

namespace Sunder.Markup.Abstract;

public interface IComponentBuilder
{
    public SundexComponent CreateComponent(SundexDocument layout, ISundexContext context);
}