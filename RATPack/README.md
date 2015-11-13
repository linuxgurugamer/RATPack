After noticing a sharp rise in crashes Gene Kerman commissioned a study on why rockets and planes crash. The resulting 3,151 page report was turned over to SatNet Aerospace to research and develop parts that could reduce crashes or make them more survivable. The first parts to come out of the project were Ram Air Turbines and the group was quickly dubbed “The RAT Pack”. Even as they expanded into more flight safety parts the name stuck.


# The RAT Pack:

##RATs
The RAT Pack provides Ram Air Turbines that provide power from flowing air. I was partly inspired by a documentary on the "Gimli Glider", a 767 that had to glide to a landing with nothing but batteries and a RAT to power critical systems. A couple of spaceplane landings where I had to constantly manage electric charge to maintain control and keep enough for the landing gave me some incentive to actually build it. The RATs are designed to provide emergency power and have peak power at or slightly above that of an RTG. Electric charge output is a function of both airspeed and atmospheric density.

- RAT-1 "Pinwheel" Ram Air Turbine – As basic as they come, it provides little power and must be activated manually.


- RAT-360 "Cage Master" Ram Air Turbine – Generates more power and will automatically start up when electric charge is depleted. Extremely stable power output over a wide range of speed and atmospheric densities.

- RAT-720 "Topper" Ram Air Turbine - A RAT built into a nosecone. It has a higher peak power than the RAT-360, but has a performance curve similar to the RAT-1. 

- RAT-6000 "Pack Master" Ram Air Turbine – The most powerful RAT. This one has the ability to control electric charge flowing from its internal battery and turns it on only when it is activating. It is able to generate peak power over a broader range.

##Thrust Reversers
Thrust Reversers redirect the engine thrust. They are used to reduce stopping distance once landed. I developed it primarily for Duna spaceplanes where a short stopping distance is a huge asset. They will redirect the thrust of the engine attached to them. Attaching multiple engines will not give you the desired behavior as only one engine will be reversed. 

- TR-1 “Backup” Thrust Reverser – Designed to fit a Turbojet or RAPIER engine.

- TR-L “Way Back” Thrust Reverser – Designed to fit longer 1.25 m engines, particularly the LV-N.

## Terrain Awareness and Warning Systems
A Terrain Awareness and Warning Systems (TAWS) gives you a chance to avoid a crash by warning you while you still have some time to react. It uses the radar altimeter and some calculations to figure out what the terrain looks like. It also has an obstacle detecting radar (FLT Radar), however it only activates below a certain altitude and uses a fair amount of power when in use. 

-TAWS-1 "Lookout" Warning System - Equipped with Ground Proximity Warning System (GPWS) and Forward Looking Terrain (FLT) Radar. 

# Questions and Answers:

Q: Are these realistic?

A: Probably not. RATs really do exist and they do have these basic capabilities, but I just tried to make the operating envelope useful and fun, I have no idea what it should be. They probably should not survive the speeds or temperatures they are allowed to.

Q: Where can I use them?

A: Anywhere with an atmosphere. Kerbin, Duna, Eve, Laythe are all good places to use them. Jool and Kerbol might work, but you'll probably be crushed or melted before you get any power.

Q: What happens if I run out of power landing on the Mun with a RAT?

A: You will probaby hit the Mun and the RAT will be destroyed. You need an atmosphere.

Q: What happens if I run out of power landing at the KSC with a RAT?

A: If you have an auto-activating RAT it will deploy and start generating power. If it is a manual RAT, you can't activate it after losing power, so prepare for high speed lithobraking.

Q: Do they work inside fairings or cargo bays?

A: No, they need to be in the air-stream.

Q: How does orientation affect RATs?

A: Right now it doesn't, though that may change in the future.

Q: Can I fly backwards using a thrust reverser?

A: Yes, though it is not recommended. Most planes and rockets are not aerodynamically stable in both directions.

Q: How much thrust will a thrust reverser reverse?

A: Short answer – you lose a lot of thrust, but it should be enough to stop quickly. Long Answer – Each TR has a thrust modifier which defaults to 75% intended to account for thrust lost to exhaust escaping to the sides. Additionally the thrust direction is not directed in reverse, but in a 45 degree angle above and below the engine. The net thrust is in reverse, but there are significant losses from the thrust directed up and down that cancels out. 

Q: Will a thrust reverser work in space?

A: It will, though you are effectively reducing your engine efficiency, so you are better off re-orienting.

Q: Why doesn't my descent rate match my vertical speed?

A: They are similar but different values. Descent rate is probably better described as ground closure rate. It is how quickly you are approaching the ground. Vertical speed is how quickly you are changing altitude relative to a common reference radius (i.e. sea-level). If you are on level terrain they will be the same. If however the ground is rising relative to sea-level you will have a higher ground closure rate than vertical speed which is a really important distinction when deciding whether you will hit the ground at some point in the near future.

Q: Will I always have time to pull up when TAWS issues a warning?

A: Not necessarily. The current values are very... "Kerbal", they give you a chance at survival, but no guarantees. If you aren't trying to hit terrain it is usually enough warning to avoid the occasional near miss, but if you deliberately try to test the limits you will hit the ground at least some of the time. You can set your own values, but I found anything that would ensure survival was too conservative for the way I fly in game.

#Configuring TAWS
## Presets
There are a number of presets available. If you don't want to configure each parameter yourself you can use one of them.

- Safety First - As you might expect this is a very conservative profile. It is actually less safe than what the FAA requires in real life, but for the game it is generally adequate.
- Default - This is what the parts starts configured at. This will tolerate more aggressive landings than Safety first.
- Fearless - You need to have the reaction times of a superb pilot like Jeb or Val to survive when you get a warning under this configuration.
- Widowmaker - Designed for aggressive maneuvers this gives very little warning which avoids false positives, but also means that if you're not already pulling up when the warning goes off you're probably not going to survive.

## Manual
A little background is necessary. The way the GPWS function works is it keeps track of the height from the ground over time and uses that to calculate the descent rate or ground closure rate. This is how quickly you are approaching the ground irrespective of whether you are going down or the ground is coming up (which is why it is not the same as vertical speed). If the descent rate is faster than it should be the TAWS issues a warning. 

- Landing Speed - This is the speed at which TAWS will warn you at 0 meters.
- Max Altitude - This is the highest altitude at which TAWS will issue a warning.
- Max Speed - This is the maximum speed (descent rate) you can have at the maximum altitude without getting a warning. Higher values mean you can approach the ground faster without a warning, smaller value mean you will get a warning more readily.

The easiest way to describe how these relate is to think of a graph with descent rate in one axis and altitude in the other. Landing speed defines a point at 0 m and max speed defines the other point at max altitude. If you draw a straight line between these points the TAWS will issue a warning when you cross this line.

- Linear Inclusion - The forward looking terrain radar will find terrain that is too far to either side for you to hit. This is how far to either side from the forward line (from the part)  an object will be considered for a warning.
- Landing Tolerance - If you have your landing gear deployed the max speed parameter will be multiplied by this value. The assumption being that if you deployed your landing gear you are coming in for landing and know you will be approaching the ground (meant to reduce false positives while landing).

The forward looking terrain radar is designed to handle a blind spot in the GPWS function by scanning ahead for obstacles and warning when you are approaching them faster than you should (the same threshold equation is used for this as the GPWS function with the distance used in place of the altitude). In real life this is served by a database of known obstacles (EGPWS) and GPS location, but this approach seemed better suited to exploring the unknowns. 