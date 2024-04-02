using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;

namespace ThirtyDollarGUI.Services;

public class DialogService : AvaloniaObject
{
    private static readonly Dictionary<object, Visual> RegistrationMapper = new();

    public static readonly AttachedProperty<object?> RegisterProperty =
        AvaloniaProperty.RegisterAttached<DialogService, Visual, object?>(
            "Register");

    static DialogService()
    {
        RegisterProperty.Changed.Subscribe(RegisterChanged);
    }

    public static void SetRegister(AvaloniaObject element, object value)
    {
        element.SetValue(RegisterProperty, value);
    }

    public static object? GetRegister(AvaloniaObject element)
    {
        return element.GetValue(RegisterProperty);
    }

    private static void RegisterChanged(AvaloniaPropertyChangedEventArgs<object?> e)
    {
        if (e.Sender is not Visual sender)
            throw new InvalidOperationException("The DialogService can only be registered on a Control");

        // Unregister any old registered context
        if (e.OldValue.Value != null) RegistrationMapper.Remove(e.OldValue.Value);

        // Register any new context
        if (e.NewValue.Value != null) RegistrationMapper.Add(e.NewValue.Value, sender);
    }

    public static Visual? GetVisualForContext(object context)
    {
        return RegistrationMapper.TryGetValue(context, out var result) ? result : null;
    }

    public static TopLevel? GetTopLevelForContext(object context)
    {
        return TopLevel.GetTopLevel(GetVisualForContext(context));
    }
}