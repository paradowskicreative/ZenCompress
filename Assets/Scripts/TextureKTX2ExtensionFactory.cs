using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GLTF.Schema {
    public class TextureKTX2ExtensionFactory : ExtensionFactory {
        public const string EXTENSION_NAME = "KHR_texture_basisu";
        public const string SOURCE = "source";
        public ImageId Source;

        public TextureKTX2ExtensionFactory (ImageId source) {
            ExtensionName = EXTENSION_NAME;
            Source = source;
        }

        public override IExtension Deserialize(GLTFRoot root, JProperty extensionToken) {
            ImageId source = new ImageId();
            if(extensionToken != null) {
                JToken sourceToken = extensionToken.Value[SOURCE];
                source = sourceToken != null ? sourceToken.Value<ImageId>("source") : source;
            }
            return new MozHubsTextureBasisExtension(source);
        }
    }
}

