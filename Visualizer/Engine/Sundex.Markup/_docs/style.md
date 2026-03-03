# The Default Styling Language of Sundex

This Markdown document explains what the `engine` style language of Sundex contains.

## Types of Style Definitions

These are the definitions of style types ordered by priority:

- Component
- Class
- ID

ID overrides Class, Class overrides Component.

## Style Definition Syntax

```
component my-component {
    Width = 100px;
}

class my-class {
    BackgroundColor = #000;
}

id my-id {
    FontSize = 12px;
    FontColor = #fff;
}
```

## Available Style Properties

### All Components

- Width (allows px, % and auto)
- Height (allows px, % and auto)
- BackgroundColor (uses Hex for basic backgrounds and !gradient for gradients)

### Positioning Elements

- Padding (allows px only)
- Spacing (allows px only, used when there are multiple children)
- Margin (allows px only)
- Direction (horizontal or vertical)
- Align (Start, Center, End, Stretch)

### Text Elements
- FontSize (allows px only)
- FontColor (uses Hex)

### Usage of Logic Variables

Logic variables can be exported using the Export() method.
The underlying value can be accessed using the .Value property.
The wrapper for exported values is called `StyleValue<T>` where T is the type of the value.
StyleValue<T> implements IStyleValue.

```
component my-component {
    Width = !var "my-width";
    Height = !var "my-height";
}

```