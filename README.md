# ThirtyDollarWebsiteConverter

Hello! If you're reading this then you're searching for things related to the Thirty Dollar Website https://thirtydollar.website/ and found this project. 

This project is an experiment (which isn't completed yet, to see the things that I've done look at the bottom of the README) to see how to work with PCM audio. It takes a .🗿 file and uses it's data to form a playable track. 

(Will be added in the future, as it now outputs raw PCM instead of the WAVE format) 

(yes I know that it takes 44 bytes, it's not that important to me now)

To use the audio made by this project, you need to open it in Audacity using Import > Raw Data, and using the settings: 

"Encoding: Signed 16-bit PCM", 

"Byte order: Little Endian", 

"Channels: 1", 

or use it in something else that supports raw PCM.
 
If you want to help, please feel free to submit pull requests, as I am rather incompetent in working with PCM samples and audio in general.


Sources for the included .🗿 files:

Radiotomatosauce99 - "big shot [Deltarune].🗿" Link: https://www.youtube.com/watch?v=_D9RL5X4c2M

Radiotomatosauce99 - "It has to be this way [Metal Gear Rising Revengeance].🗿" Link: https://www.youtube.com/watch?v=3ISh6lAK0kI

Radiotomatosauce99 - "watery graves [Plants vs. Zombies].🗿" Link: https://www.youtube.com/watch?v=cAANIc7RPhs

Xenon Neko - "catastrophe_tdw_v2.🗿" Link: https://www.youtube.com/watch?v=UqqMvkD1QMg

Me - other sequence files.





Things that are currently working great or not so great:

Serializing the composition: Finished.

Timing the track: Done, but really funky at the moment.

Audio output quality: Garbage, constantly clipping. Sounds like Fishers' Price toys.

Loops and things: Not stable, constantly cause errors with the result.

Changing speed of samples: Works, but needs fine tuning to get the octaves right.

Changing volumes: Not implemented, yet.
