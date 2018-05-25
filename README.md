# Trash Dash Emote

Trash Dash Emote is a game built for part 2 of the IGGI Game Development training workshop (Goldsmiths, 14th-25th May 2018).

It is a endless runner built/hacked from an example Unity games called Trash Dash. Trash Dash Emote has three modes, one where the game level is generated randomly and two where the level is generated based on the players affective state.  


## Overview
Trash Dash Emote features 14 hand-crafted track sections. Each of these represents one line of obstacles -  trash cans, barriers, rats etc.   


## Experience Driven Procedrual Content Generation

This project is inspired by the principal of Experience-Driven PCG [1]

## PCG as a Bandit Problem 

## Visual Model of Affect

## Game-Play Based Model of Affect
Another approach is to the model the players experience through game play meassures. In the case of Trash Dash Emote this is carried out by tracking three features - numbers of keys pressed, percentage of coins collected, and if the player 'tripped' (lost a life) or not. Each of these are summed and scaled so that the maximum reward is 1 and the minimum is 0. This is then plugges straight into the exploit variable in the UCB1 function. 

It is interesting to discuss the number of keys pressed as a measure of experience. A game where no keys are required to be pressed is trivially easy and therefore no-fun whereas a game where there are a lot of keys to be pressed in a short time is complicated and difficult and therefore unfun. In order to choose the 'optimal' number of keys to pressed the game was played many times and a number, 2 per section, was chosen emerically as feeling 'about right'. Therefore the score for the amount of keys pressed give maximum reward if 4 keys were pressed over the two segments considered. 

## Development

## References
[1] Georgios N. Yannakakis and Julian Togelius. 2011. Experience-Driven Procedural Content Generation. IEEE Transactions on Affective Computing 2, 3.
[2] Peter Auer, Nicolò Cesa-Bianchi, and Paul Fischer. 2002. Finite-time Analysis of the Multiarmed Bandit Problem. Mach. Learn. 47.
[3] Cameron Browne. 2013. UCT for PCG. IEEE Conference on Computational Intelligence in Games (CIG).
