using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;

namespace ThirtyDollarGUI.Behaviors;

public class LogScrollBehavior : Behavior<ListBox>
{
    protected override void OnAttached()
    {
        base.OnAttached();

        if (AssociatedObject is null) return;
        AssociatedObject.AutoScrollToSelectedItem = true;
        AssociatedObject.SelectionMode = SelectionMode.AlwaysSelected;

        AssociatedObject.Items.CollectionChanged += (_, args) =>
        {
            if (args.Action is not NotifyCollectionChangedAction.Add) return;
            AssociatedObject.SelectedIndex = AssociatedObject.Items.Count - 1;
        };
    }
}