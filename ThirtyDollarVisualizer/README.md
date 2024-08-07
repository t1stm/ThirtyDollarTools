# Thirty Dollar Visualizer

An OpenGL program that visualizes Thirty Dollar Covers.

[![Animated WebP of the visualizer playing "Haunted House" covered by "mossan_"](../Screenshots/visualizer-demo.webp)](https://twitter.com/kasetu_238/status/1699764122572001468)

## Usage

If you want to use this program without modifying any settings, just open the
ThirtyDollarVisualizer executable for your current operating system, which you get at the
[releases tab](https://github.com/t1stm/ThirtyDollarTools/releases).

If it's your first time opening the program, wait for all sounds to finish downloading and then use it.
![Animated WebP of the visualizer downloading samples and playing "Bling-Bang-Bang-Born" covered by "cyvos"](../Screenshots/visualizer-demo-2.webp)

I'll now showcase all available launch options for the more advanced users.

### Launch Options

| Short Name | Long Name              | Description                                                                                               | Default Value |
|------------|------------------------|-----------------------------------------------------------------------------------------------------------|---------------|
| `-i`       | `--sequence`           | Sets the sequence the visualizer will start playing when opened.                                          | unset         |
| `-w`       | `--width`              | Sets the width of the window.                                                                             | 1600          |
| `-h`       | `--height`             | Sets the height of the window.                                                                            | 840           |
| `-c`       | `--camera-follow-mode` | Mode: Possible values are "tdw" or "line".                                                                | "tdw"         |
| `-f`       | `--fps-limit`          | Sets the FPS limit of the window. If unset, the window uses VSync. If set to 0, the fps limit is removed. | unset         |
| `-s`       | `--scale`              | Changes the zoom/scale of all sequence objects.                                                           | 1.0           |
|            | `--audio-backend`      | Changes the audio backend. Possible values are "bass" or "openal".                                        | "bass"        |
|            | `--no-audio`           | Disables all audio processing and playback.                                                               | unset         |
|            | `--greeting`           | Changes the greeting at the start of the visualizer.                                                      | unset         |
|            | `--event-size`         | Changes how big the events are in pixels.                                                                 | unset         |
|            | `--event-margin`       | Changes how big the gap is between events in pixels.                                                      | unset         |
|            | `--line-amount`        | Changes the maximum amount of events that can be on a single line.                                        | unset         |


