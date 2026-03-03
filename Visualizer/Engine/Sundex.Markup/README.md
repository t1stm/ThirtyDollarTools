# Sunder.Markup

A UI markup language for the (future name: Sundex) project.
(initially Sunder, decided on a whim, and means to break into multiple parts)
(replaced er with ex because it looks like S &(und) EX in German, so haha funny name)

# Goals

Incredibly simple HTML-style markup language.

```xml
<sundex version="1.0" requires="[base, controls, custom-component]">
  <style language="sundex">
    class bigButton {
      Width = 100px;
    }
    
    id firstButton {
      Background = #001122AA;
    }
    
    component button {
      Background = #998877DD;
    }
  </style>
  
  <logic language="csharp" context-hint="MyCustomComponentClass" using="[System]">
    <!-- For more information check the dedicated logic language documentation: ./docs/logic.md -->
    [!<![CDATA[
    
    MyCustomComponentClass data = Context.Data;
    
    TrackedState<T> buttonValue = State.Get<T>(id: "", bind: "firstButton");
    buttonValue.Assert<string>();

    string value = buttonValue.Value;
    data.MyProperty = 123; // not sure why you'd want to do this, but it's possible

    data.OnFinalizeButtonClick = () => {
      data.MyProperty++;
      buttonValue.Value = $"Clicked! {data.MyProperty} times";
    }
        
    ]]>
  </logic>
  
  <layout>
    
    <button id="firstButton" class="bigButton">
      <label value="" />
      <background />
    </button>
    
  </layout>
</sundex>
```

# Components

The base building components are defined as C# classes first but other components that import the base components can be defined in the markup.
Here's an example of a component that is defined in markup:

```xml
<sundex version="1.0" component="custom-component" part-of="[custom-components, other-custom-stuff]" requires="[base]">
  <!-- <logic> and <style> are optional for all custom components -->
  <style language="sundex">
    
  </style>
  
  <layout>
    <flex direction="horizontal" spacing="5px">
      <label value="Hello World!" />
    </flex>
  </layout>
</sundex>
```

# Style

Used to define the style of the UI.

The style language is defined by the `language` attribute.
By default, the engine will use and only provide the `engine` style language, 
but other languages can be implemented for other use cases.

Defined in the `<style>` tag.
More info on the style language can be found in the [style language documentation](./docs/style.md).

# Logic

Used to add reactivity to the UI.
The logic language is defined by the `language` attribute.
By default, the engine will use and only provide the `csharp` logic language, 
but other languages can be implemented for other use cases.

Defined in the `<logic>` tag.
More info on the logic language can be found in the [logic language documentation](./docs/logic.md).

# Connections

## Order of Execution

1. Parse Layout
2. Run Logic
3. Apply Style

