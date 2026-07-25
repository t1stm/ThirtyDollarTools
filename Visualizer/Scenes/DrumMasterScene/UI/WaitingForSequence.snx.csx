using Sundex.Components.Labels;

var context = As<DrumMaster>(Context);

Component.GetID<Button>("back-button").OnClick += _ => context.Game.SceneManager.TransitionTo("home");
