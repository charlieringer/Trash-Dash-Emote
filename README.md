# Trash Dash Emote

Trash Dash Emote is a game built for part 2 of the IGGI Game Development training workshop (Goldsmiths, 14th-25th May 2018).

It is an endless runner built/hacked from an example Unity game called Trash Dash. Players must run through the world, collecting coins and powerups and avoiding obstacles with the goal of running the longest and collecting the most coins and thus getting the highest score. Trash Dash Emote has three modes, one where the game level is generated randomly and two where the level is generated based on the player's experience.  

Whilst this work was developed for a training module is the purpose, as designed, is to be used in an experiment. As such there are various game modes, each providing a different PCG method. The game also outputs data about the player's emotions in each frame as well as the average values when the PCG generator is run. This should give a researcher, interested in this area of PCG, a game which can be used "as is" to run an experiment and generate rich data about the player's experience. 

## Overview
Trash Dash Emote features 14 hand-crafted track sections. Each of these represents one line of obstacles -  trash cans, barriers, rats etc. As the player runs through the world a track section is selected, generated, and then added to the end of the "track" queue. The goal of this games is to experiment with ways in which the selection can be modified based on the player's experience. There are 3 game modes, Random, Visual, Gameplay-based.

- In the Random mode (0) track pieces are generated randomly, with no thought to which pieces the player enjoys. 
- In the Visual mode (1) the player's webcam is used to track the players face. This data is then passed to the Affectiva Affdex library for emotion recognition. The result of this is then used to change the likelihood of track segments being generated
- In the Gameplay-based mode (2) the track generation is adaptive as in the visual mode but, in this case, we score track pieces based off in-game metrics rather than the player's visual display of emotion. 

### Running the game
This project, as provided here, requires Unity (2017) to run. Load this project as a Unity project and build. Builds have been tested for Mac but no other platforms. 

## Experience-Driven Procedural Content Generation
This project is inspired by the principals of Experience-Driven PCG [1]. Once a piece of content its generated it is evaluated based on the player's experience and this knowledge is then used on subsequent iterations of the PCG loop with the aim of improving the content generated in a way which adapts to the player's preferences. 

### PCG as a Bandit Problem 
As mentioned above there are 14 track sections. For each track piece, I am interested in finding the optimal piece to generate next based on the player's experience. However, because measuring experience in a game setting is difficult and noisy the system which learns that the optimal mapping must be slow to adapt and be keen on exploring other option. Therefore it is possible to model this level generation as a many-armed bandit problem where for each track piece we consider n arms where n is the number of possible track pieces that can be generated next. In the case of Trash Dash Emote, there are 14 bandits, one for each track section, each with 14 arms, as each track section is capable of generating any other section.

Once we have PCG modelled in this way we can used the UCB1[2] function to learn the best explotation and exploration parameters for each of these bandits. UCB1 has the form: 

<p align="center">
<img src="https://latex.codecogs.com/gif.latex?%5Cbar%7Bx_i%7D%20&plus;%20c%5Csqrt%7B%5Cfrac%7B%5Cln%7B%28N_i%29%7D%7D%7Bn_i%7D%7D">
  </p>

In this project, we consider average player experience, as modelled below to be the exploitation parameter, and retain the same exploration parameter. This system is not dissimilar to the systems proposed in [3].

### Visual Model of Affect
For the visual model of the player's affect, the player's webcam is used along with the Affdex tool to model player emotion. Each frame the game stores the Valence and Engagement scores and then, when a section has been completed, these values are averaged, rescaled and summed to a range between -5 - 10 (this is lopsided because valence is scored from -100 - 100 and engagement is scored from 0 - 100). This value is then passed into a logistic function to transform it non-linearly into a value between 0 and 1. This non-linear transformation is required because often the values whilst playing games hover around 0 or have generally low values and therefore the reward would not change much for these. The average of these scores, bound between 0 - 1, is then used as the exploit measure in the UCB1 function described above. 

### Game-Play Based Model of Affect
Another approach is to the model the players experience through gameplay measures. In the case of Trash Dash Emote this is carried out by tracking three features - numbers of keys pressed, the percentage of coins collected, and if the player 'tripped' (lost a life) or not. These are summed and scaled so that the maximum reward is 1 and the minimum is 0. This is then plugged straight into the exploit variable in the UCB1 function. 

It is interesting to discuss the number of keys pressed as a measure of experience. A game where no keys are required to be pressed is trivially easy and therefore no-fun whereas a game where there are a lot of keys to be pressed in a short time is complicated and difficult and therefore unfun. In order to choose the 'optimal' number of keys to pressed the game was played many times and a number, 2 per section, was chosen empirically as feeling 'about right'. Therefore the score for the number of keys pressed gives the maximum reward if 4 keys were pressed over the two segments considered as less reward as the number of keys moves away from this. 

## Output Data
When a user plays the game 2 files are stored. A frame by frame file stores various per-frame affective values; Joy, Sadness, Angry, Surprise, Disgust, Fear, Contempt, Valence, and Engagement. It also has some metadata such as how many games the player has played and which section they are currently experiencing. 

The Summary file output one row each time a new track piece is generated (at the end of a section) and details average affective values across the section as well out data about the best UCB1 values and which track pieces were generated. 

## Development
Development for this project was very fluid as there was only one person working on it and therefore no need to communicate and organise work between a team. It also meant that whenever work was blocked by a different task then the blocker and the blockee are the responsibility of the same person. Broadly the development was carried out in this manner:

- Week 1: General research and designer regarding both the game and algorithm
- Monday 21st - Game development, PCG hooks created, cruft removed
- Tuesday 22nd - Visual model implemented and hooked up, some preliminary testing
- Wednesday 23rd - Gameplay based model designed, implemented and hooked up, more testings
- Thursday 24th -Tweaks, improvements, testing, data outputting and bug fixes
- Friday 25th - Documentation and presentations

## Reflections and Limitations
During development, I noticed that the Affdex model is not sensitive to the emotions of game players. This is mostly because players often do not have a very strong display of emotions. Using a logistic function was designed to aid this but ultimately I felt that it was worth experimenting with different models, hence the game-play based one.

The system requires a lot of gameplay, at least 196 sections but realistically more, to evaluate each piece ones. This means the player needs to play for a long time before the system starts generated adapted content. Furthermore, because this project was built from an example game designed to highlight all of the Unity features there is a large amount of cruft which seems to cause performance issues. It is possible that in future creating something from scratch would have been easier but the use of prebuilt assets does lend a polished feel to the game. 

There are also two major issues with this Bandit-based PCG design:
 - Delayed Evaluation - Content if generated a long time before the player experiences it and therefore it is possible that a player can die before evaluation. This means that certain track pieces are not experienced until long into the game.
- Repeated Sections – If Section A -> Section A is seen as the “best” segment it will be generated a lot before these content is experienced and therefore evaluated. This causes long periods of very boring and samey gameplay.

## Demo
A demo can be seen at https://youtu.be/Gz30Z6CmeFE

## References
[1] Georgios N. Yannakakis and Julian Togelius. 2011. Experience-Driven Procedural Content Generation. IEEE Transactions on Affective Computing 2, 3.

[2] Peter Auer, Nicolò Cesa-Bianchi, and Paul Fischer. 2002. Finite-time Analysis of the Multiarmed Bandit Problem. Mach. Learn. 47.

[3] Cameron Browne. 2013. UCT for PCG. IEEE Conference on Computational Intelligence in Games (CIG).
