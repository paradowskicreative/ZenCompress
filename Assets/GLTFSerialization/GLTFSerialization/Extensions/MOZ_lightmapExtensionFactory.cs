using System;
using Newtonsoft.Json.Linq;
using GLTF.Math;
using Newtonsoft.Json;
using GLTF.Extensions;

namespace GLTF.Schema
{
	public class MOZ_lightmapExtensionFactory : ExtensionFactory
	{
		public const string EXTENSION_NAME = "MOZ_lightmap";
		public const string INTENSITY = "intensity";
        public const string INDEX = "index";
        public const string TEXCOORD = "texCoord";

		public MOZ_lightmapExtensionFactory()
		{
			ExtensionName = EXTENSION_NAME;
		}

		public override IExtension Deserialize(GLTFRoot root, JProperty extensionToken)
		{
			float intensity = 1;
            TextureInfo lightmapInfo = new TextureInfo { Index = new TextureId { Id = 0, Root = root }, TexCoord = 0 };

			if (extensionToken != null)
			{
                JToken intensityToken = extensionToken.Value[INTENSITY];
                intensity = intensityToken != null ? (float)intensityToken.DeserializeAsDouble() : intensity;

                JToken lightmapInfoToken = extensionToken.Value[INDEX];
                lightmapInfo.Index.Id = lightmapInfoToken != null ? lightmapInfoToken.DeserializeAsInt() : lightmapInfo.Index.Id;

                JToken texCoordToken = extensionToken.Value[TEXCOORD];
                lightmapInfo.TexCoord = texCoordToken != null ? texCoordToken.DeserializeAsInt() : lightmapInfo.TexCoord;
			}

            return new MOZ_lightmapExtension(intensity, lightmapInfo);

		}
	}
}
