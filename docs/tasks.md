# ThirtyDollarTools Improvement Tasks Checklist

Below is an ordered, actionable checklist to improve the repository across architecture, quality, performance, testing, tooling, UX, documentation, and release. Each item is intended to be independently checkable. Tackle them in order unless otherwise justified.

1. [ ] Create a high-level architecture document (docs/architecture.md) covering solution layout, project responsibilities, and data flow (Parser -> Converter/Audio -> Visualizer/GUI/CLI). Include dependency graph and boundaries.
2. [ ] Align target frameworks: decide on net9.0 as the single TFM or define multi-targets where needed (e.g., CLI/Library), and update all .csproj to be consistent. Document rationale in architecture doc.
3. [ ] Centralize package version management with Directory.Packages.props at repository root; move package versions from each .csproj into it. Pin exact versions to ensure reproducibility.
4. [ ] Establish repository-wide coding standards via .editorconfig (naming, formatting, analyzers’ severities). Ensure Rider/VS and CI respect it.
5. [ ] Enable nullable reference types in all projects (ensure <Nullable>enable</Nullable>) and fix or annotate all nullable warnings. Introduce nullability checks for external inputs and file paths.
6. [ ] Turn on or tighten analyzers across all projects: add Microsoft.CodeAnalysis.NetAnalyzers and (optionally) StyleCop.Analyzers; tune rules and gradually elevate key rules to warnings-as-errors.
7. [ ] Replace public mutable fields in Parser event types (BaseEvent, NormalEvent, etc.) with properties and/or immutable records. Provide explicit constructors and builder or With-pattern to maintain ergonomics.
8. [ ] Improve exception hygiene: avoid generic NullReferenceException/ArgumentOutOfRangeException without messages. Provide meaningful messages or use domain-specific exceptions; prefer Try*/Result patterns for non-exceptional control flow.
9. [ ] Audit BaseEvent.Stringify/NormalEvent.Stringify for edge cases: null SoundEvent, invalid ValueScale combinations, and unexpected event codes. Add input validation and unit tests for all branches (e.g., !bg, !pulse, !divider).
10. [ ] Standardize logging: introduce an abstraction (ILogger or custom) consumed across Visualizer, Converter, Parser; consolidate DefaultLogger usage; ensure consistent log levels, structured messages, and optional JSON output for CLI.
11. [ ] Add cancellation and progress to long-running operations (PCMEncoder.GetAudio*, GenerateAudioData, ProcessChannel): propagate CancellationToken, avoid blocking UI threads, and document cooperative cancellation.
12. [ ] Reduce allocations in PCMEncoder hot paths: adopt ArrayPool<T>/MemoryPool for buffers, use spans (Span<T>/ReadOnlySpan<T>) where appropriate, and minimize per-sample boxing/closures in loops. Benchmark changes.
13. [ ] Validate thread-safety in Visualizer render/update loops: ensure RenderBlock usage cannot deadlock; verify no awaits while the semaphore is held and that input events can’t contend indefinitely.
14. [ ] Review OpenGL context management: confirm Context.MakeCurrent is necessary per-frame; if not, move to OnLoad or resize paths. Filter GL debug messages by severity; allow runtime toggle for GL_KHR_debug callback.
15. [ ] Evaluate upgrading OpenTK packages from prerelease 5.0.0-pre.x to the latest stable (or pin to a specific pre-release) and test across Windows/Linux/macOS. Document any API changes.
16. [ ] Harden scene lifecycle: expand IScene to include OnUnload/Dispose and pass a CancellationToken through Init/Start/Update to coordinate shutdown. Document lifecycle contract.
17. [ ] Make Scenes collection immutable after startup (e.g., read-only or copy) to prevent mutation during runtime; guard with thread-safe access if background tasks can add scenes.
18. [ ] Add argument validation and error feedback for file drop and CLI paths; ensure Visualizer gracefully handles missing or malformed sequence files and displays actionable messages.
19. [ ] Introduce dependency injection where it adds clarity (e.g., for logging, settings, file system, audio backends) without overcomplicating lightweight projects. Keep composition roots in app projects (GUI/Visualizer/CLI/Bot).
20. [ ] Consolidate configuration management: use strongly typed settings with validation (e.g., IOptions pattern for GUI/Visualizer) and environment override support. Provide a sample config file in docs/examples/.
21. [ ] Improve unit test coverage for Parser (parsing rules, Stringify/ToString, copy/clone correctness), Converter (placement, mixing math, WAV header), and Audio edge cases (clipping, volume scaling boundaries).
22. [ ] Add integration tests that load small example sequences end-to-end through Parser -> PCMEncoder to produce short audio; verify with deterministic assertions (e.g., known length, RMS, hashes with tolerance).
23. [ ] Expand ThirtyDollarBenchmarks to include encoder micro-benchmarks for RenderSample, mixing loops, and multi-thread slice counts. Track regressions with Baseline comparisons.
24. [ ] Introduce continuous integration (GitHub Actions): build matrix for Windows/Linux/macOS, run unit tests, collect coverage (coverlet), publish coverage report artifact, and cache NuGet.
25. [ ] Add static analysis to CI (dotnet format, analyzers) and enforce .editorconfig. Fail on new critical warnings (configurable threshold) to prevent regressions.
26. [ ] Establish release workflows: publish self-contained single-file builds for CLI/Visualizer (win-x64, linux-x64, osx-x64/osx-arm64), attach to GitHub Releases, and include checksums.
27. [ ] Package example assets for Visualizer (fonts/shaders/textures) and verify that the build symlink/copy target behaves correctly on Windows and Unix; add smoke tests to ensure assets load.
28. [ ] Review and optimize SixLabors ImageSharp usage (disposal, pooling) in Visualizer; ensure images/fonts are loaded once and reused; dispose resources deterministically.
29. [ ] Add graceful audio backend fallback: detect if OpenAL/BASS resources are missing and provide clear instructions or fallback modes. Capture initialization failures with actionable messages.
30. [ ] Document developer setup (README update): prerequisites, building per project, running tests/benchmarks, Visualizer hotkeys (e.g., F2 shader reload), and troubleshooting (GL drivers, audio devices).
31. [ ] Improve CLI UX: add --help examples, validation for incompatible flags, and exit codes; include verbose/quiet modes and JSON logging for automation.
32. [ ] Strengthen DiscordBot reliability: handle rate limits, transient failures with retries/backoff, and configuration secrets via environment variables or user secrets; add minimal integration tests using fakes.
33. [ ] Add XML documentation summaries for all public APIs in Parser/Converter libs and enable documentation file generation in .csproj; optionally publish to docs site via DocFX or similar.
34. [ ] Normalize namespaces and folder structure (e.g., avoid spaces in folder names like "Custom Events"); update namespaces to match folder hierarchy consistently.
35. [ ] Introduce guard utilities for frequently repeated checks (null/empty paths, ranges, argument validation) to standardize error handling and messages.
36. [ ] Replace magic numbers with named constants or configuration (e.g., default volumes, buffer sizes, seek timeouts). Document units and ranges.
37. [ ] Evaluate and, if beneficial, split large classes (e.g., ThirtyDollarApplication ~1k lines) into smaller components (input handling, rendering, subscriptions, UI/overlay), with cohesive responsibilities.
38. [ ] Add performance tracing hooks (EventSource or simple stopwatch logs) around encoding and rendering hotspots; expose optional tracing via CLI flags or debug UI overlay.
39. [ ] Ensure proper disposal of unmanaged resources: GL objects, audio contexts, streams, writers; audit using IDisposable and using declarations across projects.
40. [ ] Create a migration/deprecation plan for ThirtyDollarConverter.Migrate (if still needed): document usage, inputs/outputs, and consider merging into CLI or removing if obsolete.
41. [ ] Set up pre-commit checks or git hooks (optional): run dotnet format, basic tests on staged changes to reduce CI noise (document opt-in usage).
42. [ ] Add security considerations: validate external file inputs to prevent path traversal or oversized/untrusted content; run static analysis for unsafe code blocks where enabled (AllowUnsafeBlocks=true in Visualizer).
43. [ ] Add contribution guidelines updates: coding style, commit message conventions, PR templates, and issue templates. Reference tasks.md as the canonical roadmap.
