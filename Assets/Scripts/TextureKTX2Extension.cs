using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GLTF.Schema {
    public class TextureKTX2Extension : IExtension {
        public ImageId Source = new ImageId();
        
        public TextureKTX2Extension(ImageId source) {
            Source.Id = source.Id;
            Source.Root = source.Root;
        }

        public IExtension Clone(GLTFRoot gltfRoot) {
            return new TextureKTX2Extension(Source);
        }

        public JProperty Serialize() {
            JProperty jProperty = 
                new JProperty(TextureKTX2ExtensionFactory.EXTENSION_NAME, new JObject(
                    new JProperty(TextureKTX2ExtensionFactory.SOURCE, Source.Id)
                ));
            return jProperty;
        }
    }
}

