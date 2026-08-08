# Sundex Engine

**Sundex** (originally "Sunder", named on a whim — German pun on **S**(und)**EX**, also meaning "to break into multiple parts") is the in-house C# / .NET 10 game engine that powers the Visualizer. It is built around three deliberate design pillars:

1. **Simplicity** — a small, readable codebase that one person can hold in their head.
2. **Multithreading-friendly** — work that doesn't need the GL thread can be done off it (asset I/O, file enumeration, logic compilation), and GL state changes are funnelled back via deferred queues.
3. **Declarative UI** — UIs are described in [XML markup](Markup/Markup.md) + a [custom CSS-like DSL](Style%20DSL/Style%20DSL.md) + embedded C# scripting, all parsed at runtime.

Built on top of:

- **OpenTK 5.0-pre** — OpenGL bindings, windowing, input.
- **MSDF-Sharp** — multi-channel signed distance field rasterisation for high-quality scalable text.
- **SixLabors.ImageSharp** — image decoding and pixel manipulation.
- **Serilog** — structured logging with per-context loggers.
- **Microsoft.CodeAnalysis.CSharp.Scripting** (Roslyn) — runtime C# compilation for embedded `<logic>` blocks.

## Top-level layout

```
Sundex/
├── Sundex.Core/                    Pure C# building blocks (Animations, Renderable base, stopwatches)
├── Sundex.Engine/                  Game loop, asset pipeline, renderer, text, scenes, threading
├── Sundex.Components/              UIElement-based components (panels, labels, buttons, scrollbars, ...)
├── Sundex.Markup/                  XML-style markup parser + C# scripting bridge
├── Sundex.Style.DSL/               CSS-like style sheet language (parser + values + animations)
├── Tests/                          xUnit tests for parsers and components
└── Debug/                          Dummy projects used during engine development
```

## Sections

The engine is split into four reading paths. Read them in this order if you are new to the codebase — each builds on the previous:

1. [Engine](Engine/Engine.md) — the runtime: the `Game` window, asset pipeline, GPU resource management, the renderer, scenes, text, threading. **This is the foundation.**
2. [Components](Components/Components.md) — the `UIElement` tree: panels, labels, buttons, layout, hit-testing, animation hooks. **This is what gets rendered.**
3. [Markup](Markup/Markup.md) — the XML-style language that turns a `.snx.xml` document into a tree of components, with embedded C# logic. **This is what users write.**
4. [Style DSL](Style%20DSL/Style%20DSL.md) — the CSS-like styling language that decorates components and drives animations. **This is what makes things look right.**

## How a frame happens (10-second tour)

```
                            ┌────────────────────────┐
.snx.xml file               │  Game (GameWindow)     │
       │                    │  ──────────────────    │
       ▼                    │  OnUpdateFrame ───────►│ SceneManager.Update / Mouse / Keyboard
MarkupParser                │                        │
       │                    │  OnRenderFrame ───────►│ SceneManager.Render
       ▼                    │                        │   └──► Scene.Render
SundexDocument              │                        │          └──► UIContext.Render
       │                    │                        │                 └──► Renderable.Render(camera)
       │ ComponentBuilderV1 │                        │
       ▼                    │                        │
UIElement tree   ◄─── style sheet ◄── StyleParser
       │
       │ runLogic(this)  (Roslyn-compiled C#)
       ▼
Bound, styled UI
```

## What lives where

| Concern | Project | Read |
|---|---|---|
| Game loop, input, GL context | `Sundex.Engine` | [Entrypoint](Engine/Entrypoint.md) |
| GPU buffers, shaders, textures | `Sundex.Engine` | [Renderer](Engine/Renderer/Renderer.md) |
| Loading files / shaders / textures from disk + assemblies | `Sundex.Engine` | [Asset Management](Engine/Asset%20Management.md) |
| MSDF text rendering | `Sundex.Engine` | [Text Rendering](Engine/Text%20Rendering/Text%20Rendering.md) |
| Scenes (stacking, transitions) | `Sundex.Engine` | [Scene Management](Engine/Scene%20Management.md) |
| Off-thread work + exception marshalling | `Sundex.Engine` | [Threading](Engine/Threading.md) |
| `UIElement`, panels, labels, animations on UI | `Sundex.Components` | [Components](Components/Components.md) |
| XML parse, C# scripting, building components | `Sundex.Markup` | [Markup](Markup/Markup.md) |
| Style sheets, selectors, gradients, keyframes | `Sundex.Style.DSL` | [Style DSL](Style%20DSL/Style%20DSL.md) |

## Testing the engine

The `Sundex/Tests/` project exercises the parsers and component layout independently of a GL context. The `Sundex/Debug/` directory contains throwaway sandboxes (`Sundex.Engine.DummyProject`, `Sundex.Markup.Debug`) used during development to spin up a window and try things.
</content>
</invoke>