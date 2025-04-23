using SixLabors.Fonts;
using ThirtyDollarVisualizer.Objects.Planes;

namespace ThirtyDollarVisualizer.UI;

public static class Interface
{
    public static FlexPanel TopBar(int height)
    {
        return new FlexPanel(0, 0, 0, height)
        {
            Background = new ColoredPlane((0.2f, 0.2f, 0.2f, 1f)),
            Direction = LayoutDirection.Horizontal,
            Spacing = 10,
            Padding = 5,
            VerticalAlign = Align.Center,
            AutoWidth = true,
            Children =
            [
                new Label("Thirty Dollar Editor")
                {
                    FontStyle = FontStyle.Bold,
                    UpdateCursorOnHover = true
                },
                new StackPanel
                {
                    Background = new ColoredPlane((0.2f, 0.2f, 0.8f, 1f)),
                    AutoWidth = true,
                    AutoHeight = true,
                }
            ]
        };
    }

    public static UIElement Main()
    {
        return new FlexPanel(0,0,0,0)
        {
            Background = new ColoredPlane((0.3f, 0.3f, 0.3f, 1f)),
            AutoHeight = true,
            AutoWidth = true,
            Padding = 10,
            Children = [
                
            ]
        };
    }
}