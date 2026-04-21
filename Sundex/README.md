# Sundex

Sundex is the custom game engine and UI framework used by the Thirty Dollar Visualizer. It is built on top of OpenTK and provides a set of tools for 2D rendering, asset management, and styling.

## Project Structure

The Sundex directory is organized into several modules:

### [Sundex.Engine](./Sundex.Engine)
The core engine implementation.
- **Rendering**: OpenGL-based rendering system using OpenTK.
- **Asset Management**: Loading and managing textures, fonts, and shaders.
- **Scene Management**: Base classes for creating and managing game scenes.
- **Threading**: Utils for managing game and render loops.

### [Sundex.Core](./Sundex.Core)
Core abstractions and base types used across the engine.
- `Renderable`: Base class for anything that can be rendered.
- `Animations`: Support for property animations and easing.
- `SeekableStopwatch`: Specialized timing utilities.

### [Sundex.Components](./Sundex.Components)
A library of reusable UI components and game elements built using the Sundex engine.

### [Sundex.Style.DSL](./Sundex.Style.DSL)
A Domain Specific Language for styling Sundex components, providing a clean and declarative way to define visual properties.

### [Sundex.Markup](./Sundex.Markup)
Support for defining UI and layouts using markup, including integration with C# scripting for dynamic behavior.

### [Debug](./Debug)
Contains dummy projects and debug tools for engine development.
- `Sundex.Engine.DummyProject`: A minimal project for testing engine features.
- `Sundex.Markup.Debug`: Tools for debugging the markup system.

### [Tests](./Tests)
Unit and integration tests for various Sundex modules.

## Technologies Used
- **C# 14.0 / 15.0**
- **.NET 10.0**
- **OpenTK (OpenGL)**
- **Serilog** (Logging)
- **MSDF-Sharp** (Multi-channel signed distance field text rendering)
