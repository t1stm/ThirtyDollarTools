/* Hi hello this is a small console app made to reverse engineer sounds. It's just an idea for now but if I want I'll work on it.
    TODO: this needs a good sound difference calculator. without it this will fall apart fairly easily.
    ChatGPT also recommended a "Genetic algorithm" but I am not sure for this

    TODO: precompute sounds and store them in a cache.
    TODO: use SIMD vectors
    TODO: don't write bugs :D
*/

Console.WriteLine("Sound Engineering Tool 3000tm 💯 🔥🔥🔥");

// precompute sounds' pitches
const float minPitch = -32.0f; /* Dropping this any lower will result in huge sound sizes. */
const float maxPitch = 60.0f;

var pitchDifference = 0.1f; /* TODO: get from stdin arguments */
var panningDifference = 0.1f; /* TODO: get from stdin arguments */
var targetSound = "./search.wav"; /* TODO: get from stdin arguments */