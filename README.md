# ZenCompress
A fine-grain GUI texture compression tool for glTF created by Ethan Michalicek and [Paradowski Creative](https://paradowski.com). Read more about this tool and its benefits in [our introductory blog post]().

## How to use
ZenCompress operates on "glTF Separate" exports from Blender, where a geometry .bin, a human-readable .gltf meta file, and texture files are all exported separately. As an example asset, we'll open [this .blend file](./docs/Handle_Glove_v004.blend), which contains test geometry and .png textures for compression.

![image](./docs/image.png)

In most recent Blender versions, go to File > Export > glTF 2.0, and select "glTF Separate," optionally including a folder name to stash textures (otherwise textures are included in the same path as the exported .gltf file).

![image](./docs/export-settings.png)

Once exported, open either our Windows or Mac version of the release executable, and click the `...` elipses button next to "IMPORT" to select the .gltf file you've just exported

![image](./docs/gltf-open.png)

After selecting the file, click the "Import glTF" button to import it into the compression tool. Once it loads, feel free to select compression settings, and toggle compression per-texture (or use the texture type buttons to toggle compression for all Diffuse textures, or all Lightmaps, etc). Users can select between ETC1S which encodes faster and has a higher compression ratio, or UASTC, which can handle transparency and produces fewer artifaces on gradient-like images (learn more about these distrinctions in our introductory blog post).

![image](./docs/gltf-glove.png)

Once your desired settings are selected, click the `...` elipses button next to "EXPORT" to name your export .glb file. Once named, click "Export .glb" to begin the compression and conversion process. The "INFO" log will give you progress indicators - the larger the image, the longer encoding will take.

![image](./docs/gltf-export.png)

The output can then be loaded into, for example, A-Frame's KTX2 loader example, found in that repo at `./examples/test/model.html`, or any three.js or A-Frame project that uses the KTX2 loader script.

![image](./docs/running.png)
 
 ## Credits and Licensing
 ZenCompress was created by Ethan Michalicek for Paradowski Creative, and has been open sourced under the MIT license attached in this repo. README documentation and the introductory blog post by James C. Kane.