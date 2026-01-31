# ThirtyDollarConverter.Next

This will be the next version of the ThirtyDollarConverter which aims to fix some design flaws 
that prevent it from being used in the Visualizer efficiently.

The goals will be to make it more usable in an editor-like environment and to decouple the code.

## Planned Features

- [ ] Difference tracker between two Placements
  - This will take an already encoded audio, remove the missing differences and add new ones if they exist.
  - This means that the encoded audio needs to be represented in some way as a dictionary that contains all encoded sounds into it.
  - This will also allow for the editor to be able to add or remove sounds to the dictionary.