I'm refactoring a game I've prototyped before. The game was extremely fun, but difficult to manage where the core architecture wasn't very organised or clean.

I'd like to start it again, this time working through the systems cleaning. I'd like for you to suggest a sensible structure for the project, and to help me refactor it.

The game is made in an engine called sbox. We can use the following resources:
sbox forum: https://sbox.game/f
documentation: https://sbox.game/dev/doc/

api reference: https://sbox.game/api

The engine also recently went open source. You can find the repository here: https://github.com/Facepunch/sbox-public

I am a novice programmer, so if possible prioritise simple, readable code instead of overly complex solutions or programming patterns.

The game is called "Punt" it's a simple flicking football game where you flick pieces at a ball to try and score a goal. The premise is quite simple, but I'd like to make it highly polished, in the way nintendo games may have a simple concept but be extremely satisfying and fun to play.

The player uses a mouse or controller to select/grab pieces. Once a piece is grabbed the user can pull backwards to determine the flick strength and direction. Once they let go, the piece is flicked in the direction of their pull. While a piece is grabbed, the game enters slow motion to give the player a chance to make their play. Once a piece has been flicked that piece goes on cooldown for a short period.

It is important that the input design is done in a way that can support mouse and/or controller input.

The game is primarily a multiplayer game, featuring 1v1 and 2v2 modes. In multiplayer, everyone can use any piece on their team, provided it hasn't already been grabbed by a teammate. Rounds last a set time, and whoever is in the lead at the end of the game wins. In the case of a draw, the game goes into overtime where the first to score wins. Once a goal is scored, we should have a short replay of the goal (which can be cancelled by players, think Rocket League). After the replay, the pitch resets and we kick off again.

For now, we should make networking server authoritative to make things simple. In the future we will need to explore simulating physics on the client and being predictive if possible.

The game will feature several modes - Singleplayer (vs a bot), ranked 1v1, ranked 2v2 and custom games. In the future we should plan around having a "training/challenge mode" (which could be scoring from a certain gamestate within a certain time, again like rocket league) or even other game modes like a basketball mode, mode with powerups etc so lets keep things as extendible as possible.

Ranked should be the main appeal, the game will feature a simple ELO system using sbox's stats back-end.

Ranked will use a primitive queue system, while custom games involve setting up a lobby and inviting friends. We should use sbox's party system to tie into any queues/custom games so that players can queue together easily and/or join custom games together.

A large driver in the game will involve collecting points by playing, these will have no effect on games themselves, or your rank, but will allow you to unlock certain cosmetics and trinkets etc. Primarily you will be collecting pieces for your team by buying and opening packs - there will be a variety of characters of varying rarity to collect. You should also be able to customise the ball itself, trails from the ball and player pieces etc. We will eventually need to track things like goals, assists, shots on target etc to generate these points.