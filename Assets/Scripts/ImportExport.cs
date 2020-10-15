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
using System;
using System.Threading;
using System.Text.RegularExpressions;
using System.Linq;
using System.Diagnostics;
using UnityEngine.UI;
using TMPro;


public class ImportExport : MonoBehaviour
{
    // public string GLTFUri;
    // public GLTFRoot root;
    public TextMeshProUGUI loadedText;
    public Button importButton;
    public Button exportButton;

    public string importFilePath;
    public string exportFilePath;
    public bool useExistingBasis = false;

    // public string path;
    // public string fileName;

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
    private Stream loadedGltfStream;
    private FileLoader binLoader;
    private Stream loadedBinStream;
    private FileLoader imgLoader;
    private Stream loadedImgStream;
    private BinaryWriter imageWriter;
    private Stream compiledStream;
    
    public GLTFRoot gltfRoot;
    public GLTFRoot glbRoot = new GLTFRoot();
    private GLTFBuffer buffer;
    private BufferId bufferId;
    protected GLBStream gltfStream;
    private string directoryPath;

    private Process process;
    
    public void SetImportPath(string path) {
        importFilePath = path;
        importButton.interactable = path != "" ? true : false;
    }

    public void SetExportPath(string path) {
        exportFilePath = path;
        exportButton.interactable = CanExport();
    }

    public async Task Import() { await LoadGLTF(false); }

    public async Task Export() { await ExportGLB(); }

    public async Task LoadGLTF(bool withPreview) {
        try {
            exportButton.interactable = false;
            importButton.interactable = false;
            var time = Time.time;
            await LoadStream();
            if(withPreview)
                await LoadModel();
            Logging.Log("Loaded in " + Mathf.RoundToInt((Time.time - time) * 100f) / 100f + " seconds.\n");
            loadedText.text = "Loaded: " + Path.GetFileName(importFilePath);
            importButton.interactable = CanImport();
            exportButton.interactable = CanExport();
        } catch(Exception err) {
            Logging.Log(err.Message);
        } finally {
            importButton.interactable = CanImport();
            exportButton.interactable = CanExport();
        }
    }

    private async Task ExportGLB() {
        try {
            exportButton.interactable = false;
            importButton.interactable = false;
            var time = Time.time;
            await ExportStream();
            Logging.Log("Exported in " + Mathf.RoundToInt((Time.time - time) * 100f) / 100f + " seconds.\n");
            importButton.interactable = CanImport();
            exportButton.interactable = CanExport();
        } catch(Exception err) {
            Logging.Log(err.ToString());
        } finally {
            importButton.interactable = CanImport();
            exportButton.interactable = CanExport();
        }
    }
    
    private bool CanExport() { return !string.IsNullOrEmpty(exportFilePath) && gltfRoot != null ? true : false; }

    private bool CanImport() { return !string.IsNullOrEmpty(importFilePath) ? true : false; } // Reduncancy for future extensibility.
    
    private async Task LoadModel() {
        // TODO: Load the model
    } 

    private async Task LoadStream() {
        directoryPath = Path.GetDirectoryName(importFilePath);
        var filename = Path.GetFileName(importFilePath);
        gltfLoader = new FileLoader(directoryPath);
        compiledStream = new MemoryStream();

        await LoadJson(filename);
        if(!gltfRoot.IsGLB) {
            var binPath = Path.Combine(directoryPath, gltfRoot.Buffers[0].Uri);
            binLoader = new FileLoader(binPath);
            await LoadBin(binPath);
        }

        imgLoader = new FileLoader(directoryPath);
    }

    private async Task AddImage(GLTFImage image, BinaryWriter output, FileLoader loader, int bufferViewId, uint additionalLength) {
        Logging.Log("Adding '" + image.Uri + "' to .glb...");
        var loaderStream = await loader.LoadStreamAsync(image.Uri);
        loaderStream.Position = 0;
        uint offset = (uint)output.BaseStream.Position + additionalLength;
        var length = GLTFSceneExporter.CalculateAlignment((uint)loaderStream.Length, 4);
        GLTFSceneExporter.CopyStream(loaderStream, output);

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

    private void PrepGLBRoot() {
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
    }

    private async Task ExportStream() {
        PrepGLBRoot();
        var fullPath = exportFilePath;//Path.Combine(exportFilePath, fileName) + ".glb";

        var jsonStream = new MemoryStream();
        var binStream = new MemoryStream();
        var imgStream = new MemoryStream();

        var bufferWriter = new BinaryWriter(binStream);
        var imgWriter = new BinaryWriter(imgStream);
        var jsonWriter = new StreamWriter(jsonStream, Encoding.ASCII) as TextWriter;

        loadedBinStream.Position = 0;
        GLTFSceneExporter.CopyStream(loadedBinStream, bufferWriter);
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
        glbRoot.Serialize(jsonWriter, false);

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
        loadedGltfStream = await gltfLoader.LoadStreamAsync(jsonFilePath);
        await loadedGltfStream.CopyToAsync(compiledStream);

        GLTFParser.ParseJson(compiledStream, out gltfRoot, 0);
    }

    private async Task LoadBin(string binFilePath) {
        loadedBinStream = await binLoader.LoadStreamAsync(binFilePath);

        await loadedBinStream.CopyToAsync(compiledStream);
    }

    private async Task Convert(CancellationToken token) {

        if(!useExistingBasis) {
            foreach(var image in glbRoot.Images) {
                token.ThrowIfCancellationRequested();
                image.Uri = image.Uri.Replace("%20", " ");
                Logging.Log("Converting image '" + image.Uri + "' to BASIS...");
                var ttc = Time.time;
                var exe = Path.Combine(Application.streamingAssetsPath, "basisu.exe");//.Replace('/', '\\');
                var uriSplit = image.Uri.Split(new char[] {'\\', '/'}).ToList();
                var fileName = uriSplit[uriSplit.Count - 1];
                var outputDir = Path.Combine(directoryPath, image.Uri.Substring(0, image.Uri.Length - fileName.Length));
                var args = "-output_path " + outputDir + " -file " + "\"" + Path.Combine(directoryPath, image.Uri) + "\"";//.Replace('/', '\\');
                await RunProcessAsync(exe, args);
                var ext = image.MimeType.ToLower().Replace("image/", "");
                image.MimeType = "image/basis";
                Regex rgx = new Regex(@"\.(?:.(?!\.))+$");
                image.Uri = image.Uri.Replace(rgx.Match(image.Uri).Value, ".basis");
                ttc = Time.time - ttc;
                Logging.Log("Finished in " + Mathf.RoundToInt(ttc * 100f) / 100f + " seconds.\n");
            }
        }

        foreach(var texture in glbRoot.Textures) {
            var moz = new MozHubsTextureBasisExtension(texture.Source ?? new ImageId());

            if(texture.Extensions == null)
                texture.Extensions = new Dictionary<string, IExtension>();

            if(!texture.Extensions.ContainsKey(MozHubsTextureBasisExtensionFactory.EXTENSION_NAME))
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

        var bufferWriter = new BinaryWriter(binStream);
        TextWriter jsonWriter = new StreamWriter(jsonStream, Encoding.ASCII);

        var buffer = new GLTFBuffer();

        buffer.ByteLength = (uint)bufferWriter.BaseStream.Length;

        glbRoot.Serialize(jsonWriter, true);

        bufferWriter.Flush();
        jsonWriter.Flush();

        GLTFSceneExporter.AlignToBoundary(jsonStream);
        GLTFSceneExporter.AlignToBoundary(binStream, 0x00);

        int glbLength = (int)(GLTFHeaderSize + SectionHeaderSize +
            jsonStream.Length + SectionHeaderSize + binStream.Length);

        // string fullPath = Path.Combine(path, Path.ChangeExtension(fileName, "glb"));


        using (FileStream glbFile = new FileStream(exportFilePath, FileMode.Create))
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

        Logging.Log(stringBuilder.ToString());
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