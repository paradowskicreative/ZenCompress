using GLTF.Math;
using GLTF.Schema;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace GLTF.Schema {
    public class MOZ_lightmapExtension : IExtension
    {
        public float Intensity = 1f;
        public TextureInfo LightmapInfo;


        public MOZ_lightmapExtension(float intensity, TextureInfo lightmapInfo)
        {
            Intensity = intensity;
            LightmapInfo = lightmapInfo;
        }

        public IExtension Clone(GLTFRoot gltfRoot)
        {
            return new MOZ_lightmapExtension(Intensity, new TextureInfo(LightmapInfo, gltfRoot));
        }

        public JProperty Serialize()
        {
            JProperty jProperty =
                new JProperty(MOZ_lightmapExtensionFactory.EXTENSION_NAME,
                    new JObject(
                        new JProperty(MOZ_lightmapExtensionFactory.INTENSITY, Intensity),
                        new JProperty(MOZ_lightmapExtensionFactory.INDEX, LightmapInfo.Index.Id),
                        new JProperty(MOZ_lightmapExtensionFactory.TEXCOORD, LightmapInfo.TexCoord)
                    ));

            return jProperty;
        }
    }
}
