using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GLTF.Schema {
    public class MozHubsTextureBasisExtension : IExtension {
        public ImageId Source = new ImageId();
        
        public MozHubsTextureBasisExtension(ImageId source) {
            Source.Id = source.Id;
            Source.Root = source.Root;
        }

        public IExtension Clone(GLTFRoot gltfRoot) {
            return new MozHubsTextureBasisExtension(Source);
        }

        public JProperty Serialize() {
            JProperty jProperty = 
                new JProperty(MozHubsTextureBasisExtensionFactory.EXTENSION_NAME, new JObject(
                    new JProperty(MozHubsTextureBasisExtensionFactory.SOURCE, Source.Id)
                ));
            return jProperty;
        }
    }
}

