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
using System.Threading;
using System.Text.RegularExpressions;
using System.Linq;
using System.Diagnostics;


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

    private FileLoader gltfLoader;
    private FileLoader binLoader;
    private FileLoader imgLoader;
    private BinaryWriter imageWriter;
    private Stream compiledStream;
    
    public GLTFRoot gltfRoot = new GLTFRoot();
    public GLTFRoot glbRoot = new GLTFRoot();
    private GLTFBuffer buffer;
    private BufferId bufferId;
    protected GLBStream gltfStream;
    private string directoryPath;

    private Process process;
    
    // Start is called before the first frame update
    private async void Start()
    {
        var time = Time.time;
        await Load();
        print("Loaded and exported in " + (Time.time - time) + " seconds.");
    }

    public async Task Load() {
        GLTFUri = GLTFUri.TrimStart(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
        string fullPath;
        fullPath = Path.Combine(Application.streamingAssetsPath, GLTFUri);//.Replace('/', '\\');
        directoryPath = URIHelper.GetDirectoryName(fullPath);//.Replace('/', '\\');
        // print(directoryPath);
        // var asyncCoroutineHelper = gameObject.AddComponent<AsyncCoroutineHelper>();
		gltfLoader = new FileLoader(directoryPath);
        // var sceneImporter = new GLTFSceneImporter(Path.GetFileName(GLTFUri), gltfLoader, asyncCoroutineHelper);
        // await sceneImporter.LoadSceneAsync();
        compiledStream = new MemoryStream();
        // gltfStream = new GLBStream {Stream = gltfStream, StartPosition = gltfStream.Position};

        await LoadJson(fullPath);
        glbRoot = new GLTFRoot(gltfRoot);
        buffer = new GLTFBuffer();
        glbRoot.Buffers = new List<GLTFBuffer>();
        bufferId = new BufferId {
            Id = glbRoot.Buffers.Count,
            Root = glbRoot
        };
        glbRoot.Buffers.Add(buffer);
        glbRoot.IsGLB = true;

        if(glbRoot.Samplers == null)
            glbRoot.Samplers = new List<Sampler>();
        
        Sampler sampler;
        if(glbRoot.Samplers.Count == 0) {
            sampler = new Sampler {
                MagFilter = MagFilterMode.Linear,
                MinFilter = MinFilterMode.Nearest,
                WrapS = GLTF.Schema.WrapMode.Repeat,
                WrapT = GLTF.Schema.WrapMode.Repeat
            };

            glbRoot.Samplers.Add(sampler);
        } else {
            foreach(var smplr in glbRoot.Samplers) {
                smplr.MinFilter = MinFilterMode.Nearest;
            }
        }
        
        if(!gltfRoot.IsGLB) {
            var binPath = Path.Combine(directoryPath, gltfRoot.Buffers[0].Uri);
            binLoader = new FileLoader(binPath);
            await LoadBin(binPath);
        }
        imgLoader = new FileLoader(directoryPath);
        
        // GLTFParser.ParseJson(compiledStream, out glbRoot, 0);
        // ExportJson();

        await ExportStream();
        // GLBBuilder.ConstructFromGLB(glbRoot, compiledStream);

        PrintJson();

    }

    private async Task AddImage(GLTFImage image, BinaryWriter output, FileLoader loader, int bufferViewId, uint additionalLength) {
        print("Adding '" + image.Uri + "' to .glb...");
        await loader.LoadStream(image.Uri);
        loader.LoadedStream.Position = 0;
        uint offset = (uint)output.BaseStream.Position + additionalLength;
        // print("Offset: " + offset);
        var length = GLTFSceneExporter.CalculateAlignment((uint)loader.LoadedStream.Length, 4);
        // print("Length: " + length);
        GLTFSceneExporter.CopyStream(loader.LoadedStream, output);
        // length = GLTFSceneExporter.CalculateAlignment(length, 4);
        var bufferImg = glbRoot.Images.First((img) => img.Uri == image.Uri);
        bufferImg.Uri = null;
        bufferImg.BufferView = new BufferViewId{
            Id = bufferViewId,
            Root = glbRoot
        };
        

        var bufferView = new BufferView {
            Buffer = bufferId,
            ByteLength = length,
            ByteOffset = offset
        };
        
        glbRoot.BufferViews.Add(bufferView);
    }

    private async Task ExportStream() {
        var fullPath = Path.Combine(path, fileName) + ".glb";

        var jsonStream = new MemoryStream();
        var binStream = new MemoryStream();
        var imgStream = new MemoryStream();

        var bufferWriter = new BinaryWriter(binStream);
        var imgWriter = new BinaryWriter(imgStream);
        var jsonWriter = new StreamWriter(jsonStream, Encoding.ASCII) as TextWriter;
        // var buffer = new GLTFBuffer();

        binLoader.LoadedStream.Position = 0;
        GLTFSceneExporter.CopyStream(binLoader.LoadedStream, bufferWriter);
        foreach(var image in glbRoot.Images) {
            if(image.MimeType == null || image.MimeType == "") {
                var ext = image.Uri.Split('.');
                image.MimeType = "image/" + ext[ext.Length - 1].ToLower();
            }
        }

        await Convert(ThreadingUtility.QuitToken);

        var preImgByteLength = (uint)bufferWriter.BaseStream.Length;
        imageWriter = new BinaryWriter(imgStream);
        var currentBufferView = glbRoot.BufferViews.Count - 1;
        if(gltfRoot.Images != null) {
            foreach(var image in glbRoot.Images) {
                currentBufferView++;
                await AddImage(image, imageWriter, imgLoader, currentBufferView, preImgByteLength);
            }
        }
        
        GLTFSceneExporter.AlignToBoundary(imgStream, 0x00);
        buffer.ByteLength = (uint)bufferWriter.BaseStream.Length + (uint)imgWriter.BaseStream.Length;
        glbRoot.Serialize(jsonWriter, true);

        bufferWriter.Flush();
        imageWriter.Flush();
        jsonWriter.Flush();

        GLTFSceneExporter.AlignToBoundary(jsonStream);
        GLTFSceneExporter.AlignToBoundary(binStream, 0x00);
        

        int glbLength = (int)(GLTFHeaderSize + SectionHeaderSize +
            jsonStream.Length + SectionHeaderSize + binStream.Length + imgStream.Length);

        using (FileStream glbFile = new FileStream(fullPath, FileMode.Create))
        {

            BinaryWriter writer = new BinaryWriter(glbFile);
          
            writer.Write(MagicGLTF);
            writer.Write(Version);
            writer.Write(glbLength);

            writer.Write((int)jsonStream.Length);
            writer.Write(MagicJson);

            jsonStream.Position = 0;
            GLTFSceneExporter.CopyStream(jsonStream, writer);

            writer.Write((int)binStream.Length + (int)imgStream.Length);
            writer.Write(MagicBin);

            binStream.Position = 0;
            GLTFSceneExporter.CopyStream(binStream, writer);

            imgStream.Position = 0;
            GLTFSceneExporter.CopyStream(imgStream, writer);

            writer.Flush();
        }
    }

    

    private async Task LoadJson(string jsonFilePath)
    {
        await gltfLoader.LoadStream(jsonFilePath);

        await gltfLoader.LoadedStream.CopyToAsync(compiledStream);

        // gltfStream.Stream = gltfLoader.LoadedStream;
        // gltfStream.StartPosition = 0;    

        GLTFParser.ParseJson(compiledStream, out gltfRoot, 0);

        // var glb = GLBBuilder.ConstructFromGLB(gltfRoot, gltfStream.Stream);
        // print(glb.Header.FileLength);
    }

    private async Task LoadBin(string binFilePath) {
        await binLoader.LoadStream(binFilePath);
        await binLoader.LoadedStream.CopyToAsync(compiledStream);
        // GLBBuilder.ConstructFromGLB(gltfRoot, binLoader.LoadedStream);

        // GLTFParser.ParseGLBHeader()

        // await binLoader.LoadedStream.CopyToAsync(gltfStream.Stream);

    }

    private async Task Convert(CancellationToken token) {

        foreach(var image in glbRoot.Images) {
            token.ThrowIfCancellationRequested();
            image.Uri = image.Uri.Replace("%20", " ");
            print("Converting image '" + image.Uri + "' to BASIS...");
            var ttc = Time.time;
            // var pre = "/C ";
            var exe = Path.Combine(Application.streamingAssetsPath, "basisu.exe");//.Replace('/', '\\');

            var uriSplit = image.Uri.Split(new char[] {'\\', '/'}).ToList();
            var fileName = uriSplit[uriSplit.Count - 1];
            // print(fileName);
            var outputDir = Path.Combine(directoryPath, image.Uri.Substring(0, image.Uri.Length - fileName.Length));
            // print(outputDir);
            

            var args = "-output_path " + outputDir + " -file " + "\"" + Path.Combine(directoryPath, image.Uri) + "\"";//.Replace('/', '\\');
            // print(args);
            // args = args.Replace("%20", " ");
            // print(args);

            await RunProcessAsync(exe, args);
            var ext = image.MimeType.ToLower().Replace("image/", "");
            image.MimeType = "image/basis";
            Regex rgx = new Regex(@"\.(?:.(?!\.))+$");
            image.Uri = image.Uri.Replace(rgx.Match(image.Uri).Value, ".basis");
            ttc = Time.time - ttc;
            print("Finished in " + ttc + " sec");
            // print(image.Uri);
        }

        foreach(var texture in glbRoot.Textures) {
            var moz = new MozHubsTextureBasisExtension(texture.Source);

            if(texture.Extensions == null)
                texture.Extensions = new Dictionary<string, IExtension>();

            texture.Extensions.Add(MozHubsTextureBasisExtensionFactory.EXTENSION_NAME, moz);
            texture.Source = null;

            if(texture.Sampler == null) {
                texture.Sampler = new SamplerId();
                texture.Sampler.Id = 0;
            }
        }

        
        if(glbRoot.ExtensionsUsed == null)
            glbRoot.ExtensionsUsed = new List<string>();

        if(glbRoot.ExtensionsRequired == null)
            glbRoot.ExtensionsRequired = new List<string>();

        if(!glbRoot.ExtensionsUsed.Contains(MozHubsTextureBasisExtensionFactory.EXTENSION_NAME))
            glbRoot.ExtensionsUsed.Add(MozHubsTextureBasisExtensionFactory.EXTENSION_NAME);

        if(!glbRoot.ExtensionsRequired.Contains(MozHubsTextureBasisExtensionFactory.EXTENSION_NAME))
            glbRoot.ExtensionsRequired.Add(MozHubsTextureBasisExtensionFactory.EXTENSION_NAME);
        
    }

    

    private void ExportJson() {
        
        Stream binStream = new MemoryStream();
        Stream jsonStream = new MemoryStream();
        // GLBBuilder.ConstructFromGLB(gltfRoot, )


        var bufferWriter = new BinaryWriter(binStream);

        TextWriter jsonWriter = new StreamWriter(jsonStream, Encoding.ASCII);

        // print(gltfRoot.Scene);
        // gltfRoot.Scene = ExportScene(fileName, _rootTransforms);
        var buffer = new GLTFBuffer();

        buffer.ByteLength = (uint)bufferWriter.BaseStream.Length;

        glbRoot.Serialize(jsonWriter, true);

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

            if(gltfRoot.Buffers != null && gltfRoot.Buffers.Count > 0) {
                writer.WritePropertyName("buffers");
                writer.WriteStartArray();
                foreach(var buffer in gltfRoot.Buffers) {
                    writer.WriteStartObject();
                    writer.WriteProperty(buffer.Uri, "", "uri");
                    writer.WriteProperty(buffer.ByteLength, "", "byteLength");
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        print(stringBuilder.ToString());
    }

    private void OnApplicationQuit() {
        if(process != null)
            process.Kill();
    }

    private Task<int> RunProcessAsync(string fileName, string arguments) {
        var tcs = new TaskCompletionSource<int>();

        process = new Process
        {
            StartInfo = { FileName = fileName, Arguments = arguments, WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden },
            EnableRaisingEvents = true
        };


        process.Exited += (sender, args) =>
        {
            tcs.SetResult(process.ExitCode);
            process.Dispose();
            process = null;
        };

        process.Start();

        return tcs.Task;
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