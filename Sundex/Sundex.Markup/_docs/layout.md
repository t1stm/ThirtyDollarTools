# Layout

This document describes how the layout of a component is specified and how it will behave.

## Overview

Sundex.Markup must always contain a <layout> element, that should be located at the bottom of the document.

```xml
<style language="sundex">
  import "Sundex.Markup.Assets.DefaultStyleSheet.snx.ss"; // this imports the default style sheet. definitions below override it.
  
  // by default, only fields that are specified in the style are overriden from imports.
  // if you want to fully override a style, you can use the @ operator.
  component progress {
    background = "#222222";
    foreground = !gradient {
      type = linear;
      direction = 90deg; // or literal "right"
      size = 16px;
      stops = [
        0% = #ff0000,
        50% = #ffff00,
        100% = #00ff00
      ]
    }
  }
  
  @component button {
    background = #3f3f3f;
  }
</style>

<layout>
  <!-- 
    A layout can only contain one child. This will be the initial UIElement of the component. 
    The layout element itself is not rendered, but rather a container for the stuff that will be rendered. 
  -->
  <flex direction="column" spacing="5">
    <!--
      In this case the component this layout will represent is a flexbox.
      The direction and spacing attributes are set on the flexbox itself. 
    -->
    <background color="#3f3f3f"></background>
    <!-- Below are all child elements -->
    <button padding="5">
      <label value="Click me!"/>
    </button>

    <dropdown spacing="5" position-content="bottom">
      <button role="trigger">Amazing Dropdown</button>
      <flex direction="row" spacing="5" role="content">
        <!-- 
          <background> and <gradient> are pseudo-elements that set the background of the element they are placed in.
          They always override the background specified in the style and get removed from the tree once parsed. 
          Only one of them can be used per element without a specified role. By default, when no role is specified, 
          the background is set on the element itself.
          It's also worth noting that the background can be set from the style, so this way of doing it is not very optimal. 
        -->
        <background color="#222222" />

        <!-- Children start here: -->
        <button>Option 1</button>
        <button>Option 2</button>
      </flex>
    </dropdown>

    <progress>
      <!-- 
        Here the rule that "only one background can be set" is not applied since the backgrounds have roles defined. 
        The progress bar will have a gradient foreground and a dark static background. 
        If a background element with a role is specified in elements that don't support it, it will be ignored. 
      -->
      <background color="#222222" role="background"/>
      <gradient type="radial" size="16px" role="foreground">
        <stop percentage="0" color="#ff0000"/>
        <stop color="#00ff00" /> <!-- This stop has its percentage linearly interpolated, so it will be at 25% -->
        <stop percentage="50" color="#ffff00"/>
        <stop color="#00ff00" /> <!-- This one is also linearly interpolated to 75% -->
        <stop percentage="100" color="#00ff00"/>
      </gradient>
    </progress>
  </flex>
</layout>
```

Some elements accept infinite amount of children nodes, while others only accept a set of predefined children. Here is a
list of the supported elements with all their available attributes:

### Nodes that support infinite amount of children

These nodes are typically used for flexible layouts and can contain any number of child elements.

- `<stack direction="column" spacing="5">`
- `<flex direction="row" spacing="10">`
- `<panel>`

### Nodes that support a set of predefined children

These nodes have predefined layouts and can only contain a specific set of child elements. This decision is made to
simplify the markup and to avoid confusing situations like having an entire layout inside a single button.

- `<button>`
- `<dropdown>`
- `<file-picker>`
- `<progress>`

### Special nodes

These nodes only accept children of a specific type related to their purpose.

- `<gradient>` accepts only `<step>` children

### Nodes that support no children

These nodes do not accept any children, but may be used in other components. They are only modified with attributes.

- `<label>`
- `<background>`
- `<stop>`