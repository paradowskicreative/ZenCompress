using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GLTF;
using GLTF.Schema;
using UnityGLTF;
using UnityGLTF.Loader;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Text.RegularExpressions;


public class Import : MonoBehaviour
{
    public string GLTFUri;
    public GLTFRoot root;

    public string path;
    public string fileName;

    public List<string> samplers = new List<string>();
    public List<string> textures = new List<string>();
    public List<string> images = new List<string>();

    private const uint MagicGLTF = 0x46546C67;
		private const uint Version = 2;
		private const uint MagicJson = 0x4E4F534A;
		private const uint MagicBin = 0x004E4942;
		private const int GLTFHeaderSize = 12;
		private const int SectionHeaderSize = 8;

    protected struct GLBStream
    {
        public Stream Stream;
        public long StartPosition;
    }

    struct Container {
        public List<GLTFTexture> textures;
        public List<Sampler> samplers;
        public List<GLTFImage> images;
    }

    private FileLoader loader;
    
    public GLTFRoot gltfRoot = new GLTFRoot();
    protected GLBStream gltfStream;
    
    // Start is called before the first frame update
    private async void Start()
    {
       await Load();
    }

    public async Task Load() {
        GLTFUri = GLTFUri.TrimStart(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
        string fullPath;
        fullPath = Path.Combine(Application.streamingAssetsPath, GLTFUri);
        string directoryPath = URIHelper.GetDirectoryName(fullPath);
        // Debug.Log(directoryPath);
        // var asyncCoroutineHelper = gameObject.AddComponent<AsyncCoroutineHelper>();
		loader = new FileLoader(directoryPath);
        // var sceneImporter = new GLTFSceneImporter(Path.GetFileName(GLTFUri), loader, asyncCoroutineHelper);
        // await sceneImporter.LoadSceneAsync();

        // gltfStream = new GLBStream {Stream = gltfStream, StartPosition = gltfStream.Position};

        await LoadJson(fullPath);
        PrintJson();

        Convert();


        PrintJson();

        // Debug.Log(gltfRoot.Meshes[0].Name);
    }

    private void Convert() {
        foreach(var image in gltfRoot.Images) {
            var ext = image.MimeType.ToLower().Replace("image/", "");
            image.MimeType = "image/basis";
            Regex rgx = new Regex(@"\.(?:.(?!\.))+$");
            image.Uri = image.Uri.Replace(rgx.Match(image.Uri).Value, ".basis");
        }

        foreach(var texture in gltfRoot.Textures) {
            var moz = new MozHubsTextureBasisExtension(texture.Source);

            if(texture.Extensions == null)
                texture.Extensions = new Dictionary<string, IExtension>();

            texture.Extensions.Add(MozHubsTextureBasisExtensionFactory.EXTENSION_NAME, moz);
            texture.Source = null;
        }
        if(gltfRoot.ExtensionsUsed == null)
            gltfRoot.ExtensionsUsed = new List<string>();

        if(gltfRoot.ExtensionsRequired == null)
            gltfRoot.ExtensionsRequired = new List<string>();

        if(!gltfRoot.ExtensionsUsed.Contains(MozHubsTextureBasisExtensionFactory.EXTENSION_NAME))
            gltfRoot.ExtensionsUsed.Add(MozHubsTextureBasisExtensionFactory.EXTENSION_NAME);

        if(!gltfRoot.ExtensionsRequired.Contains(MozHubsTextureBasisExtensionFactory.EXTENSION_NAME))
            gltfRoot.ExtensionsRequired.Add(MozHubsTextureBasisExtensionFactory.EXTENSION_NAME);
        
    }

    private async Task LoadJson(string jsonFilePath)
    {
        await loader.LoadStream(jsonFilePath);

        gltfStream.Stream = loader.LoadedStream;
        gltfStream.StartPosition = 0;    

        GLTFParser.ParseJson(gltfStream.Stream, out gltfRoot, gltfStream.StartPosition);
    }

    private void ExportJson() {
        Stream binStream = new MemoryStream();
        Stream jsonStream = new MemoryStream();

        

        var bufferWriter = new BinaryWriter(binStream);

        TextWriter jsonWriter = new StreamWriter(jsonStream, Encoding.ASCII);

        // gltfRoot.Scene = ExportScene(fileName, _rootTransforms);
        var buffer = new GLTFBuffer();

        buffer.ByteLength = (uint)bufferWriter.BaseStream.Length;

        gltfRoot.Serialize(jsonWriter, true);

        bufferWriter.Flush();
        jsonWriter.Flush();

        GLTFSceneExporter.AlignToBoundary(jsonStream);
        GLTFSceneExporter.AlignToBoundary(binStream, 0x00);

        int glbLength = (int)(GLTFHeaderSize + SectionHeaderSize +
            jsonStream.Length + SectionHeaderSize + binStream.Length);

        string fullPath = Path.Combine(path, Path.ChangeExtension(fileName, "glb"));


        using (FileStream glbFile = new FileStream(fullPath, FileMode.Create))
        {

            BinaryWriter writer = new BinaryWriter(glbFile);

            // write header
            writer.Write(MagicGLTF);
            writer.Write(Version);
            writer.Write(glbLength);

            // write JSON chunk header.
            writer.Write((int)jsonStream.Length);
            writer.Write(MagicJson);

            jsonStream.Position = 0;
            GLTFSceneExporter.CopyStream(jsonStream, writer);

            writer.Write((int)binStream.Length);
            writer.Write(MagicBin);

            binStream.Position = 0;
            GLTFSceneExporter.CopyStream(binStream, writer);

            writer.Flush();
        }
    }

    private void PrintJson() {
        var container = new Container();
        container.textures = gltfRoot.Textures;
        container.samplers = gltfRoot.Samplers;
        container.images = gltfRoot.Images;

        StringBuilder stringBuilder = new StringBuilder();
        StringWriter stringWriter = new StringWriter(stringBuilder);

        using(var writer = new JsonTextWriter(stringWriter)) {
            writer.Formatting = Formatting.Indented;
            writer.WriteStartObject();

            if(gltfRoot.ExtensionsRequired != null) {
                writer.WritePropertyName("extensionsRequired");
                writer.WriteStartArray();
                foreach(var ext in gltfRoot.ExtensionsRequired) {
                    writer.WriteValue(ext);
                }
                writer.WriteEndArray();
            }

            if(gltfRoot.ExtensionsUsed != null) {
                writer.WritePropertyName("extensionsUsed");
                writer.WriteStartArray();
                foreach(var ext in gltfRoot.ExtensionsUsed) {
                    writer.WriteValue(ext);
                }
                writer.WriteEndArray();
            }
            
            if(container.textures != null && container.textures.Count > 0) {
                writer.WritePropertyName("textures");
                writer.WriteStartArray();
                foreach(var texture in container.textures) {
                    writer.WriteStartObject();
                    writer.WriteProperty(texture.Name, "", "name");
                    writer.WriteProperty(texture.Source?.Id, "", "source");
                    writer.WriteProperty(texture.Sampler?.Id, null, "sampler");
                    writer.WriteExtensions(texture.Extensions);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
            }

            if(container.samplers != null && container.samplers.Count > 0) {
                writer.WritePropertyName("samplers");
                writer.WriteStartArray();
                foreach(var sampler in container.samplers) {
                    writer.WriteStartObject();
                    writer.WriteProperty(sampler.Name, "", "name");
                    writer.WriteProperty(sampler.MinFilter, MinFilterMode.None, "minFilter");
                    writer.WriteProperty(sampler.MagFilter, MagFilterMode.None, "magFilter");
                    writer.WriteProperty(sampler.WrapS, GLTF.Schema.WrapMode.None, "wrapS");
                    writer.WriteProperty(sampler.WrapT, GLTF.Schema.WrapMode.None, "wrapT");
                    writer.WriteExtensions(sampler.Extensions);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
            }

            if(container.images != null && container.images.Count > 0) {
                writer.WritePropertyName("images");
                writer.WriteStartArray();
                foreach(var image in container.images) {
                    writer.WriteStartObject();
                    writer.WriteProperty(image.Name, "", "name");
                    writer.WriteProperty(image.Uri, "", "uri");
                    writer.WriteProperty(image.MimeType, "", "mimeType");
                    writer.WriteExtensions(image.Extensions);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        Debug.Log(stringBuilder.ToString());
    }
    
   
}

public static class Extensions {
    public static void WriteProperty(this JsonTextWriter writer, object property, object compare, string name) {
        if(property != compare && property != null) {
            writer.WritePropertyName(name);
            writer.WriteValue(property);
        }
    }

    public static void WriteExtensions(this JsonTextWriter writer, Dictionary<string, IExtension> extensions) {
        if(extensions != null) {
            writer.WritePropertyName("extensions");
            writer.WriteStartObject();
            foreach(var extension in extensions) {
                writer.WritePropertyName(extension.Key);
                foreach(var jprop in extension.Value.Serialize())
                    jprop.WriteTo(writer);
            }
            writer.WriteEndObject();
        }
    }
}