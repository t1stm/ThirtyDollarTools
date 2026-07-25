using Sundex.Components.Bars;
using Sundex.Components.Labels;
using Sundex.Components.Panels;

var ctx = As<PlayerBar>(Context);

ctx.RootPanel        = Component.Element                            as Panel       ?? throw new Exception("bar-root not found");
ctx.ProgressBar      = Component.RegisteredIDs["progress-bar"]      as ProgressBar ?? throw new Exception("progress-bar not found");
ctx.CurrentTimeLabel = Component.RegisteredIDs["current-time"]      as Label       ?? throw new Exception("current-time not found");
ctx.TotalTimeLabel   = Component.RegisteredIDs["total-time"]        as Label       ?? throw new Exception("total-time not found");
ctx.PlayPauseButton  = Component.RegisteredIDs["play-pause-button"] as Button      ?? throw new Exception("play-pause-button not found");
ctx.BackButton       = Component.RegisteredIDs["back-button"]       as Button      ?? throw new Exception("back-button not found");
ctx.RestartButton    = Component.RegisteredIDs["restart-button"]    as Button      ?? throw new Exception("restart-button not found");

ctx.BackButton.OnClick      = _ => ctx.OnBack();
ctx.PlayPauseButton.OnClick = _ => ctx.OnPlayPause();
ctx.RestartButton.OnClick   = _ => ctx.OnRestart();
