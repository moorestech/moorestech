# MicroVerse - Objects

With the MicroVerse Objects package installed, you can spawn objects in much the same way you can spawn trees using MicroVerse's Tree Stamp from the Vegetation module. However, trees are quite limiting in Unity's terrain system:

- They can only be rotated around the Y axis
- Scaling is limited
- They can only use primitive colliders
- They cannot contain scripts or other gameplay behaviors

However, they are quite a bit cheaper to render and store than full game objects in a scene, and for many types of objects, not just trees, a preferable choice. But when you need more, the Object module can help.

If you are familiar with the tree stamp, you will notice many of the same options. But there are new ones worth discussing.

First you can choose to hide these objects from the hierarchy, and choose which object to parent them to. If the Parent Object is not set, they will spawn on the terrain that they are above.

A minimum height is available to allow objects to float on ponds or water, even when the terrain goes lower.

The rest should seem familiar from the Tree Stamp, and if you're not familiar with the Tree stamp you can read its documentation in the main MicroVerse documentation.

Next up you'll notice the object variations area is slightly different from trees as well. Full rotation and scaling options are available.

An object can also be rotated to match the slope of the terrain.

And finally there is a sink value available to move the placed object up or down versus the terrain.

## Considerations

While you could place just about anything with the object stamp, spawning game objects is massively slower, takes more memory, and should be reserved for things that really need it. You will also notice that MicroVerse recycles the existing objects when a change is made, so any changes made manually to one of them in the editor might end up somewhere else when values are changed, or removed all together. If you need to customize an object it is better to place it manually and adjust it from there. MicroVerse will only spawn 1000 game objects per frame, so after changes occur you may see it take a few frames to finish instantiating and positioning them all.
