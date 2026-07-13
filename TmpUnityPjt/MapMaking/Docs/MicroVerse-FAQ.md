# MicroVerse - FAQ

**MICROVERSE DOES NOT EXIST AT RUNTIME - IT LITERALLY STRIPS ITSELF FROM THE SCENE ON PLAY MODE OR BUILD**

## Pre-Purchase questions

**Q: "Does MicroVerse work with X?"**
A: Most likely - MicroVerse interacts with standard Unity terrain, and does not perform any custom drawing, or take over rendering of things, etc. It can best be thought of as an editor time tool to create terrains.

**Q: "Does MicroVerse work on Mobile?"**
A: MicroVerse strips it self at runtime, except for the ambient sound system, so it doesn't have a runtime cost.

**Q: "Does MicroVerse work at runtime?"**
A: It can, but it's not supported officially. By default, MicroVerse strips itself when entering playmode or making a build, so that no stamp data is accidentally included in the build, and no processing cost is incurred.

**Q: "But what if I want to make it work at runtime anyway?"**
A: This can work, especially for things like in-game level editors. But you'll have to manage the memory usage carefully, as stamps can take a lot of memory, and the processing steps MicroVerse uses can generate a lot of internal GPU buffers. You'll also have to manage shader variants for any capabilities you want to support, and likely control when things are updated manually, and create whatever UI you want for it. That said, it makes for a very slick level editor.

**Q: "Will you help me get this working at runtime?"**
A: Beyond what's in this faq, no.

## Install issues

**Q: "I'm getting errors about missing splines when I install"**
A: Please read the readme file in the MicroVerse-Splines package which will show you how to install the Unity Splines package on 2021.3LTS

**Q: "I'm getting a shader compile issue when I install from GitHub, something about not being able to find a .cginc file"**
A: This can happen if a module is installed before the main MicroVerse module. Make sure that is installed, then right click on the other package and select reimport.

**Q: "The package manager is not installing X or throwing an error"**
A: In the last 3 years, Unity has only had brief moments where the package manager has not had crippling bugs. It's why I now allow direct github access to my repositories.

**Q: "How do I get github access?"**
A: There's a pdf in the documentation folder walking you through the steps to do this.

**Q: "I wrote to the bot and it gave me this error: 'Unable to redeem xxxx - Please download the asset via the Package Manager in the Unity Editor and try again', what's wrong?**
A: Did you try reading the error message and following the directions it's just given you?

**Q: "Is there any downside to installing from github?"**
A: Mainly that it's a live branch, and there's always the possibility that something has been broken in development or there are new features that are not quite done yet. But generally speaking I keep things as stable as possible.

**Q: "I installed 10,000 stamps into my project, and now it crashes when I select a terrain! It must be your fault!"**
A: No, Unity's terrain system loads every brush in your project when you select a terrain- at 4k resolution each, that's a ton of memory. You should either delete the Unity brushes in the project, as MV doesn't use them, or not install as many stamps. I also reported this bug in Unity, who may decide to fix it one day because it's a terrible design flaw.

## Getting Started

**Q: "How do I set the initial height of the terrain?"**
A: You can create a height stamp set to override, with no texture, and position it on the Y axis where you want the base level of the terrain.

**Q: "Part of my terrain is black!?:**
A: This happens when the terrain has no texture weights in an area. The best way to avoid this is to create a "base layer" of texture, like a base layer in photoshop. To do this, create a texture stamp as the first in the hierarchy and give it a texture, set it to global, and give it a weight of 1. This will show up wherever the weight of other textures does not total 1.

**Q: "I don't understand how texture stamps are working, they don't do what I want"**
A: The most common issue here is understanding the hierarchy order and how it applies to textures. You can think of texture stamps like photoshop layers. Stamps later in the hierarchy are applied over stamps that came before them.

**Q: "The occlusion Stamp isn't working!"**
A: The occlusion stamp prevents future stamps from having an effect on the terrain, and you have it after everything in the hierarchy. If you want to remove things that came before you in the hierarchy use the clear stamp, or move the item higher in the hierarchy, with the resulting weight value from your filters acting like the opacity of a photoshop layer.

**Q: "The Clear stamp isn't working!"**
A: The clear stamp clears previous stamps from effecting the terrain, and it has to be before things you want to effect in the hierarchy.

**Q: "I dragged X from the content browser but didn't see anything"**
A: Often things in the content browser are setup with various filters - such as working in specific height ranges or areas, so if your terrain is, say, flat, you might not see things appear if they are required at certain heights.

**Q: "I painted content with the spline tool and nothing is showing"**
A: See above

**Q: "I painted on the terrain and MicroVerse erased it!"**
A: Read the documentation about the Copy/Paste stamp.

**Q: "My tree's aren't being rendered correctly"**
A: MicroVerse does not render tree's, in fact, almost all of MicroVerse is removed at runtime, except for the ambient sound systems. Thus any rendering issues are not MicroVerse related.

**Q: "My tree's don't have physics!"**
A: Unity's terrain system doesn't support all physics types on trees, go read up on how Unity's terrain system works.

**Q: "the hole stamp isn't working!"**
A: You have to enable holes on terrains when using URP/HDRP in the SRP's settings configuration.

## General QA

**Q: "Can I get a smoothing option for height stamps?"**
A: It's actually already in there, but you will need to turn on the mip map option in the stamps texture. Once the texture has mip maps, you can blend between them to get a smoother version of the stamp.

**Q: "How can I paint an area with the Unity terrain tools?"**
A: See about the copy paste stamp in the documentation

**Q: "The preview filter doesn't match where textures/trees are being placed"**
A: The preview filter draws the terrain a few meters above showing you the weight of the filter or noise function it represents. However, that is showing weight - so you might be able to see the preview filter draw at a weight of 0.2, but your grass doesn't show up there because the terrain shader is height blending it with rock and rock is drawn instead of the grass.

**Q: "I have a grid of 644 terrains at 4k height/splat map and things are slow"**
A: First, good luck shipping that and filling it with meaningful content. But yeah, as the number of terrains and their resolution goes up, MicroVerse takes longer to process each change. That said the bottleneck is usually not actually MicroVerse, but rather rendering and Unity terrains in general. There's a number of things you can do to help with this though.

- You can work at lower resolutions, and MicroVerse will regenerate the terrains at whatever resolution you want at the end. In particular, Unity can take up to 70ms to update the terrain heightmap, mainly due to physics, on a terrain at 4k resolution. If you have 25 terrains that need updating, that's slow.
- There's a larger section about this in the documentation.
- You can not use 4k heightmaps and instead use more terrains, allowing for better culling. It will likely be faster in build too.

**Q: "Is there a way to make the detail stamps texture filter more accurate? On my grass detail I've set 'Other Texture Weight' to 0 and added a 'Grass' layer and set its weight to 1, but the grass still spawns on other texture layers in some places."**
A: Detail data in Unity is stored like a texture and has a resolution. So if your detail resolution is 512 but your texture resolution is 2048, each detail pixel covers 4x4 pixels worth of texturing, and can't possibly align to the height res splat map texturing. Further, textures in the splat map can be at very low values which are not noticeable, and the shader being used on the terrain can interpolate when the transitions happen in different ways (height blending, weight modifications, etc).

**Q: Tree's have no collision! NavMeshes generate weird around trees.**
A: Unity terrain only supports primitive colliders for tree's, not mesh colliders.

**Q: MicroVerse fails in a spectacular way in my project, but in an empty project the Demo scene works perfectly.**
A: Your best bet is to check for other third party assets that could interfere with MicroVerse. If this doesn't work, delete your Library folder, reimport the MV packages.

**Q: Stamps and/or MicroVerse edit terrains that should be out of range or not even under MV.**
A: Delete Library, reimport MV packages, refresh the Terrain's height maps by changing the resolution back and forth.

**Q: Trees, Objects, Details float above my terrain.**
A: Adjust your Prefab. Look at the examples to check your structure.

**Q: There are obvious seams between my terrains.**
A: Are you using OpenGL? Are you on Linux? These are things not fully supported by Unity and we have seen seaming issues on these platforms.

**Q: My terrain objects are at different Y heights, and they look weird.**
A: All your terrain objects have to be at zero Y height. If you want highlands for example, create a big height stamp set to override.

**Q: I have problems with addressables, textures don't load, terrain is invisible.**
A: MV strips itself at build time, or editor play time, you'll have to handle addressables yourself.

## Unity/Driver bugs

**Q: "I'm getting seams in the heightmap!"**
A: Apparently the nVidia drivers for openGL have a bug in them which causes read/writes from an Unsigned R16 textures to not work, breaking seaming between terrains. Switching to vulcan in your editor settings will work around this. (Note your game can still renderer in OpenGL just fine).

**Q: I want to convert my terrain to standard Unity Terrain, bake it.**
A: Disable the main MV component. Keep in mind that enabling it again will re-generate every stamp, so if you edit your terrain while MV is disabled, you'll lose those changes unless you Copy/Paste stamp them.

## Splines

**Q: I want my spline path to have 2 parallel lanes instead of a single path.**
A: Look at the example in the MicroVerse-Demo scene.

**Q: My spline paths don't remove trees and details from the path.**
A: Look at the examples in the MicroVerse-Demo scene.

**Q: Splines stop working when they cross terrain boundaries, and I have more than one spline in a single container.**
A: As stated in the documentation, this is not supported. Keep a single spline in a container.

**Q: My path textures don't follow the curvature of the path.**
A: Unity Terrain can not do this. MV writes texture data into the terrain, but Unity renders it.

## External Assets

**Q: "After converting my terrain to MicroSplat the terrain blending looks totally different"**
A: By default, MicroSplat uses a height based blend to transition textures when close to the camera - this lets sand get between cracks of rocks instead of just being a blurry mess between sand and rock. If you prefer the blurry mess, you can change the blending mode to "Unity Linear", or adjust the interpolation contrast setting to get more blur in the blending areas.

**Q: "I'm using MicroSplat and want to share the data across several scenes, but when I assign it the material it complains that the textures are not in the right order".**
A: This happens because you don't have the exact same textures in both scenes. If you want a "fixed set" of texture layers across multiple scenes, just include a prefab with texture stamps for each layer used at the top of your hierarchy and include it in both scenes. This will ensure that all the textures are in use, regardless of which ones are actually visible on the resulting terrain. You can then just add additional stamps to do your actual texturing.

**Q: "I'm using a streaming system and can't parent the terrains to the MicroVerse object, how can I get around that?"**
A: There's an explicit terrain list on the MicroVerse component you can use to give it the exact list of terrains, regardless of the hierarchy. Unity will allow cross scene references with an editor setting. There is also an option in the MicroVerse settings that will let you find all terrains in all scenes rather than using the hierarchy or explicit terrain list, and the Object Stamp will spawn objects in the same scene the particular terrain is in.

**Q: "I added a texture to my MicroSplat array, and MicroVerse asked me to sync and erased my texture."**
A: Read the documentation of how to use MicroVerse with MicroSplat. You do not add new texture layers directly to the MicroSplat arrays, you add them to stamps in MicroVerse and it will manage the data in the arrays.

**Q: Something goes wrong with third party renderers with trees, objects, details.**
A: Try disabling scene-culling in the MicroVerse component.

**Q: I want to edit the example shaders that come with MicroVerse.**
A: These are packed shaders. To edit them you'll need BetterShaders, the system that compiles these shaders to Unity's ShaderLab.

**Q: Stuff is wrong with terrain made in MapMagic.**
A: Make sure that MapMagic saves the TerrainData to file and also duplicate your terrain using Unity Terrain Tools. After this you should be able to create MV for existing terrains
