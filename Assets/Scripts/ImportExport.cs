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
using SixLabors.ImageSharp;

public class ImportExport : MonoBehaviour
{
	public event EventHandler<TaskCompleteArgs> TaskComplete;

	// public string GLTFUri;
	// public GLTFRoot root;
	public TextMeshProUGUI loadedText;
	public TextMeshProUGUI exportedText;
	public Button importButton;
	public Button exportButton;
	public UnityEngine.UI.Image progress;

	public string importFilePath;
	public string exportFilePath;
	public bool useExistingBasis = false;
	public bool convertLightmaps = false;
	public bool showPreview = true;
	public bool useMultithreading = true;
	public int quality = 255;
	public float threshold = 1.05f;
	public int level = 2;
	public bool preserveAlpha = false;

	public GLTFComponent gLTFComponent;
	public MouseOrbitImproved moi;

	private int queueIndex;
	private int numberOfOperations = 1;
	private int completedOperations = 0;

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

	struct Container
	{
		public List<GLTFTexture> textures;
		public List<Sampler> samplers;
		public List<GLTFImage> images;
	}

	struct Duplicate
	{
		public int source;
		public List<int> duplicates;
		public int newSource;
		public string imageName;
	}

	private FileLoader gltfLoader;
	private Stream loadedGltfStream;
	private FileLoader binLoader;
	private Stream loadedBinStream;
	private MemoryStream savedBinStream;
	private FileLoader imgLoader;
	private Stream loadedImgStream;
	private BinaryWriter imageWriter;

	public GLTFRoot gltfRoot;
	public GLTFRoot glbRoot = new GLTFRoot();
	private GLTFBuffer buffer;
	private BufferId bufferId;
	protected GLBStream gltfStream;
	private string directoryPath;

	private Stopwatch timeToCompletion = new Stopwatch();

	struct TaskData
	{
		public string name;
		public float time;
		public string msg;
	}

	struct TextureEntry
	{
		public int index;
		public string id;
		public int map;
	}

	public bool[] conversionToggles = new bool[]{
		true,  // Diffuse
		false, // Normal
		false, // Lightmap
		true,  // MetalRough
		true,  // Emissive
		true   // Occlusion
	};

	public static string pdp;

	private List<TaskData> msgQueue = new List<TaskData>();

	private List<Process> processes = new List<Process>();

	private void Start()
	{
		pdp = Application.persistentDataPath;
	}

	public void SetImportPath(string path)
	{
		importFilePath = path;
		importButton.interactable = path != "" ? true : false;
	}

	public void SetExportPath(string path)
	{
		exportFilePath = path;
		exportButton.interactable = CanExport();
	}

	public async Task Import() { await LoadGLTF(); }

	public async Task Export() { await ExportGLB(); }

	public async Task LoadGLTF()
	{
		try
		{
			exportButton.interactable = false;
			importButton.interactable = false;
			// timeToCompletion = new Stopwatch();
			// timeToCompletion.Start();

			var now = DateTime.Now;
			await LoadStream();
			var children = moi.target.GetComponentsInChildren<Transform>();
			foreach (var child in children)
			{
				if (child.gameObject.GetInstanceID() != moi.target.gameObject.GetInstanceID())
					Destroy(child.gameObject);
			}
			if (showPreview)
			{
				Logging.Log("Loading preview...");
				await LoadModel();
				children = moi.preview.GetComponentsInChildren<Transform>();
				var colliders = new List<BoxCollider>();
				Bounds bounds = new Bounds();
				foreach (var child in children)
				{
					BoxCollider boxCollider;
					if (child.gameObject.TryGetComponent<BoxCollider>(out boxCollider))
						bounds.Encapsulate(boxCollider.bounds);
				}

				if (Camera.main.aspect >= 1f)
					moi.distance = bounds.extents.magnitude * 1.5f / Mathf.Tan(Camera.main.fieldOfView * 0.5f * Mathf.Deg2Rad);
				else
					moi.distance = (bounds.extents.magnitude / Camera.main.aspect) * 1.5f / Mathf.Tan(Camera.main.fieldOfView * 0.5f * Mathf.Deg2Rad);

			}
			// timeToCompletion.Stop();
			var ttc = DateTime.Now.Subtract(now).TotalSeconds;
			Logging.Log("Loaded in " + Mathf.RoundToInt((float)ttc * 100f) / 100f + " seconds.\n");
			loadedText.text = "Loaded: " + Path.GetFileName(importFilePath);

			gltfLoader.CloseStream();
			binLoader.CloseStream();
			imgLoader.CloseStream();
			importButton.interactable = CanImport();
			exportButton.interactable = CanExport();
		}
		catch (Exception err)
		{
			Logging.Log(err.Message);
			UnityEngine.Debug.LogException(err);
		}
		finally
		{
			PopulateImageList();
			importButton.interactable = CanImport();
			exportButton.interactable = CanExport();
		}
	}

	private void PopulateImageList()
	{
		var list = new List<TextureList.Image>();
		var previousIds = new List<int>();
		for (int i = 0; i < gltfRoot.Textures.Count; i++)
		{
			var texture = gltfRoot.Textures[i];
			var id = texture.Source.Id;
			if (previousIds.Contains(id)) continue;
			previousIds.Add(id);
			var image = gltfRoot.Images[id];

			var mapType = GetMapType(i);

			var entry = new TextureList.Image()
			{
				name = image.Name,
				URI = image.Uri,
				imageIndex = id,
				mapType = mapType,
				toggled = conversionToggles[(int)mapType]
			};

			list.Add(entry);
		}

		TextureList.instance.PopulateFromImages(list);
	}

	private TextureList.MapType GetMapType(int index)
	{
		TextureList.MapType mapType = TextureList.MapType.DIFFUSE;

		// * We need to find a Material that uses a Texture (indexed) that uses our Image (indexed).

		// Funnel materials' map IDs into more digestable objects.
		var match = gltfRoot.Materials.Select((mat, i) => new Dictionary<int, int>
		{
			{0, mat.PbrMetallicRoughness?.BaseColorTexture?.Index.Id ?? -1},
			{1, mat.NormalTexture?.Index.Id ?? -1},
			{2, mat.LightmapTexture?.Index.Id ?? ((MOZ_lightmapExtension)mat.Extensions?[MOZ_lightmapExtensionFactory.EXTENSION_NAME])?.LightmapInfo.Index.Id ?? -1},
			{3, mat.PbrMetallicRoughness?.MetallicRoughnessTexture?.Index.Id ?? -1},
			{4, mat.EmissiveTexture?.Index.Id ?? -1},
			{5, mat.OcclusionTexture?.Index.Id ?? -1}
		}).FirstOrDefault(mat =>
			mat[0] == index ||
			mat[1] == index ||
			mat[2] == index ||
			mat[3] == index ||
			mat[4] == index ||
			mat[5] == index
		).DefaultIfEmpty(new KeyValuePair<int, int>(0, -1)).First(type => type.Value == index).Key;

		if (match != -1)
			mapType = (TextureList.MapType)match;

		return mapType;
	}

	private async Task ExportGLB()
	{
		try
		{
			exportButton.interactable = false;
			importButton.interactable = false;
			// timeToCompletion = new Stopwatch();
			// timeToCompletion.Start();
			var now = DateTime.Now;
			completedOperations = 0;
			await ExportStream();
			var ttc = DateTime.Now.Subtract(now).TotalSeconds;
			Logging.Log("Exported in " + Mathf.RoundToInt((float)ttc * 100f) / 100f + " seconds.\n");

			importButton.interactable = CanImport();
			exportButton.interactable = CanExport();
		}
		catch (Exception err)
		{
			Logging.Log(err.ToString());
			UnityEngine.Debug.LogException(err);
		}
		finally
		{
			importButton.interactable = CanImport();
			exportButton.interactable = CanExport();
		}
	}

	private bool CanExport() { return !string.IsNullOrEmpty(exportFilePath) && gltfRoot != null ? true : false; }

	private bool CanImport() { return !string.IsNullOrEmpty(importFilePath) ? true : false; } // Reduncancy for future extensibility.

	private async Task LoadModel()
	{
		gLTFComponent.GLTFUri = importFilePath;
		await gLTFComponent.Load();
		Logging.Log("Done loading glTF preview!");
	}

	private async Task LoadStream()
	{
		directoryPath = Path.GetDirectoryName(importFilePath);
		var filename = Path.GetFileName(importFilePath);
		gltfLoader = new FileLoader(directoryPath);
		using (gltfLoader.thisStream)
		{
			await LoadJson(filename);
			if (!gltfRoot.IsGLB)
			{
				var binPath = Path.Combine(directoryPath, gltfRoot.Buffers[0].Uri);
				binLoader = new FileLoader(binPath);
				using (binLoader.thisStream)
				{
					await LoadBin(binPath);
				}
				binLoader.thisStream.Close();
			}
		}
		gltfLoader.thisStream.Close();

		imgLoader = new FileLoader(directoryPath);
	}

	private async Task AddImage(GLTFImage image, BinaryWriter output, FileLoader loader, int bufferViewId, uint additionalLength)
	{
		Logging.Log("Adding '" + image.Uri + "' to .glb...");
		using (var loaderStream = await loader.LoadStreamAsync(image.Uri))
		{
			loaderStream.Position = 0;
			uint offset = (uint)output.BaseStream.Position + additionalLength;
			var length = GLTFSceneExporter.CalculateAlignment((uint)loaderStream.Length, 4);
			GLTFSceneExporter.CopyStream(loaderStream, output);

			var bufferImg = glbRoot.Images.First((img) => img.Uri == image.Uri);
			bufferImg.Uri = null;
			bufferImg.BufferView = new BufferViewId
			{
				Id = bufferViewId,
				Root = glbRoot
			};

			var bufferView = new BufferView
			{
				Buffer = bufferId,
				ByteLength = length,
				ByteOffset = offset
			};

			glbRoot.BufferViews.Add(bufferView);
		}
		loader.thisStream.Close();
	}

	private void PrepGLBRoot()
	{
		glbRoot = new GLTFRoot(gltfRoot);
		buffer = new GLTFBuffer();
		glbRoot.Buffers = new List<GLTFBuffer>();
		bufferId = new BufferId
		{
			Id = glbRoot.Buffers.Count,
			Root = glbRoot
		};
		glbRoot.Buffers.Add(buffer);
		glbRoot.IsGLB = true;

		if (glbRoot.Samplers == null)
			glbRoot.Samplers = new List<Sampler>();

		Sampler sampler;
		if (glbRoot.Samplers.Count == 0)
		{
			sampler = new Sampler
			{
				MagFilter = MagFilterMode.Linear,
				MinFilter = MinFilterMode.LinearMipmapLinear,
				WrapS = GLTF.Schema.WrapMode.Repeat,
				WrapT = GLTF.Schema.WrapMode.Repeat
			};

			glbRoot.Samplers.Add(sampler);
		}
	}

	private async Task ExportStream()
	{
		PrepGLBRoot();
		numberOfOperations = 1 + (glbRoot.Images != null && !useExistingBasis ? glbRoot.Images.Count : 0) * 6 + 1;
		completedOperations = 0;
		var fullPath = exportFilePath;//Path.Combine(exportFilePath, fileName) + ".glb";

		var jsonStream = new MemoryStream();
		var binStream = new MemoryStream();
		var imgStream = new MemoryStream();

		var bufferWriter = new BinaryWriter(binStream);
		var imgWriter = new BinaryWriter(imgStream);
		var jsonWriter = new StreamWriter(jsonStream, Encoding.ASCII) as TextWriter;

		savedBinStream.Position = 0;
		GLTFSceneExporter.CopyStream(savedBinStream, bufferWriter);

		foreach (var image in glbRoot.Images)
		{
			if (image.MimeType == null || image.MimeType == "")
			{
				var ext = image.Uri.Split('.');
				image.MimeType = "image/" + ext[ext.Length - 1].ToLower();
			}
		}

		completedOperations += 1;

		await Convert(ThreadingUtility.QuitToken);

		var preImgByteLength = (uint)bufferWriter.BaseStream.Length;
		imageWriter = new BinaryWriter(imgStream);
		var currentBufferView = glbRoot.BufferViews.Count - 1;
		if (gltfRoot.Images != null)
		{
			foreach (var image in glbRoot.Images)
			{
				currentBufferView++;
				await AddImage(image, imageWriter, imgLoader, currentBufferView, preImgByteLength);
				completedOperations += 1;
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

		exportedText.text = "Exported: " + Path.GetFileName(fullPath);

		completedOperations += 1;
	}

	private async Task LoadJson(string jsonFilePath)
	{
		loadedGltfStream = await gltfLoader.LoadStreamAsync(jsonFilePath);

		GLTFParser.ParseJson(loadedGltfStream, out gltfRoot, 0);
	}

	private async Task LoadBin(string binFilePath)
	{
		loadedBinStream = await binLoader.LoadStreamAsync(binFilePath);
		savedBinStream = new MemoryStream();
		await loadedBinStream.CopyToAsync(savedBinStream);
	}

	private void FixedUpdate()
	{
		progress.fillAmount = (1f / numberOfOperations) * completedOperations;
		if (msgQueue.Count == 0 || queueIndex == msgQueue.Count) return;

		for (; queueIndex < msgQueue.Count; queueIndex++)
		{
			TaskData msg = msgQueue[queueIndex];
			if (string.IsNullOrEmpty(msg.msg))
				Logging.Log("Finished " + msg.name + " in " + msg.time + " seconds.");
			else
				Logging.Log(msg.msg);
		}
	}

	private void RemoveDuplicates()
	{
		// Dictionary<int, int> remap = new Dictionary<int, int>(); // Index of duplicate, index of source.
		// List<Duplicate> duplicateMap = new List<Duplicate>();

		// // var remap2 = new List<TextureEntry>();
		// // int dupeCount = 0;
		// for (int i = 0; i < gltfRoot.Textures.Count; i++)
		// {
		// 	if (remap.ContainsKey(i)) continue;
		// 	// if(remap2.Any(entry => entry.index == i)) continue;

		// 	var tex = gltfRoot.Textures[i];

		// 	var allMatching = gltfRoot.Textures
		// 		.Select((texture, index) => new { source = i, duplicate = index, equal = texture.EqualsTexture(tex) }) // Use Select to map data to an anonymous object.
		// 		.Where(item => item.equal && item.source != item.duplicate) // Select duplicates that are not self.
		// 		.Select(item => new { item.source, item.duplicate }); // Trim the fat.

		// 	if (allMatching.Count() == 0) continue;

		// 	// dupeCount++;

		// 	var dupe = new Duplicate
		// 	{
		// 		source = i,
		// 		duplicates = new List<int>()
		// 	};
		// 	foreach (var match in allMatching)
		// 	{
		// 		if (!remap.ContainsKey(match.duplicate))
		// 		{
		// 			remap.Add(match.duplicate, match.source); // Add duplicates and their sources.
		// 			dupe.duplicates.Add(match.duplicate);
		// 		}

		// 		// 		// if (remap2.Any(entry => entry.index == match.duplicate ))
		// 		// 		// 	remap2.Add(new TextureEntry(){ index = match.duplicate, map = match.source, id = match.duplicate.ToString()});
		// 	}
		// 	duplicateMap.Add(dupe);
		// }

		// var n = 0;
		// var duplicateAggregate = new List<int>();
		// duplicateMap.ForEach(mapEntry =>
		// {
		// 	// Add to n the number of duplicates in the duplicate aggregrate that are below the source.

		// 	n = duplicateAggregate.Aggregate(0, (total, next) => next < mapEntry.source ? total + 1 : total);
		// 	print("n: " + n);

		// 	// var current = n;
		// 	mapEntry.duplicates.ForEach(duplicate =>
		// 	{
		// 		// n = duplicateAggregate.Aggregate(current, (total, next) => next < duplicate ? total + 1 : total);
		// 		// print("n: " + n);
		// 		mapEntry.newSource = Mathf.Clamp(mapEntry.source - n, 0, int.MaxValue);
		// 		duplicateAggregate.Add(duplicate);
		// 		print("source: " + mapEntry.source + " | duplicate: " + duplicate + " | new source: " + mapEntry.newSource);
		// 	});

		// 	// n += mapEntry.duplicates.Count;
		// });

	}

	private async Task Convert(CancellationToken token)
	{

		foreach (var image in glbRoot.Images)
		{
			image.Uri = image.Uri.Replace("%20", " ");
		}


		/*
		Dictionary<int, int> remap = new Dictionary<int, int>(); // Index of duplicate, index of source.
		List<Duplicate> duplicateMap = new List<Duplicate>();

		// var remap2 = new List<TextureEntry>();
		// int dupeCount = 0;
		for (int i = 0; i < gltfRoot.Textures.Count; i++)
		{
			// if (remap.ContainsKey(i)) continue;
			// if(remap2.Any(entry => entry.index == i)) continue;

			var tex = gltfRoot.Textures[i];

			var allMatching = gltfRoot.Textures
				.Select((texture, index) => new { source = i, duplicate = index, equal = texture.EqualsTexture(tex) }) // Use Select to map data to an anonymous object.
				.Where(item => item.equal && item.source != item.duplicate) // Select duplicates that are not self.
				.Select(item => new { item.source, item.duplicate }); // Trim the fat.


			var dupe = new Duplicate
			{
				source = i,
				duplicates = new List<int>()
			};
			if (allMatching.Count() != 0)
			{
				foreach (var match in allMatching)
				{
					if (!remap.ContainsKey(match.duplicate))
					{
						remap.Add(match.duplicate, match.source); // Add duplicates and their sources.
						dupe.duplicates.Add(match.duplicate);
					}

					// 		// if (remap2.Any(entry => entry.index == match.duplicate ))
					// 		// 	remap2.Add(new TextureEntry(){ index = match.duplicate, map = match.source, id = match.duplicate.ToString()});
				}

			}
			duplicateMap.Add(dupe);
			print(string.Format("Source: {0} | Dupe: {1}", dupe.source, dupe.duplicates.Count));
		}

		var n = 0;
		var duplicateAggregate = new List<int>();
		var newSourceMap = new List<Duplicate>();
		duplicateMap.ForEach(mapEntry =>
		{
			var newSourceEntry = mapEntry;

			// Add to n the number of duplicates in the duplicate aggregrate that are below the source.
			n = duplicateAggregate.Aggregate(0, (total, next) => next < mapEntry.source ? total + 1 : total);

			newSourceEntry.newSource = Mathf.Clamp(mapEntry.source - n, 0, int.MaxValue);

			var d = 0;
			mapEntry.duplicates.ForEach(duplicate =>
			{
				duplicateAggregate.Add(duplicate);
				d++;
			});

			// print("source: " + newSourceEntry.source + " | duplicate: " + d + " | new source: " + newSourceEntry.newSource);

			newSourceMap.Add(newSourceEntry);
		});

		foreach (var mat in glbRoot.Materials)
		{
			newSourceMap.ForEach(mapEntry =>
			{
				// print(string.Format("Source: {0} | Duplicates: {1} | New Source: {2}", mapEntry.source, mapEntry.duplicates.Count, mapEntry.newSource));
				if (mat.EmissiveTexture != null &&
					mapEntry.source != mapEntry.newSource &&
					(mapEntry.duplicates.Any(dupe => dupe == mat.EmissiveTexture.Index.Id) || mapEntry.source == mat.EmissiveTexture.Index.Id))
				{
					var prevIndex = mat.EmissiveTexture.Index.Id;
					var newIndex = mapEntry.newSource;
					mat.EmissiveTexture.Index.Id = newIndex;
					Logging.Log(string.Format("Replaced {0} texture source indexed {1} with texture source indexed {2}.", "emissive", prevIndex, newIndex));
				}

				if (mat.LightmapTexture != null &&
					mapEntry.source != mapEntry.newSource &&
					(mapEntry.duplicates.Any(dupe => dupe == mat.LightmapTexture.Index.Id) || mapEntry.source == mat.LightmapTexture.Index.Id))
				{
					var prevIndex = mat.LightmapTexture.Index.Id;
					var newIndex = mapEntry.newSource;
					mat.LightmapTexture.Index.Id = newIndex;
					Logging.Log(string.Format("Replaced {0} texture source indexed {1} with texture source indexed {2}.", "lightmap", prevIndex, newIndex));
				}

				if (mat.NormalTexture != null &&
					mapEntry.source != mapEntry.newSource &&
					(mapEntry.duplicates.Any(dupe => dupe == mat.NormalTexture.Index.Id) || mapEntry.source == mat.NormalTexture.Index.Id))
				{
					var prevIndex = mat.NormalTexture.Index.Id;
					var newIndex = mapEntry.newSource;
					mat.NormalTexture.Index.Id = newIndex;
					Logging.Log(string.Format("Replaced {0} texture source indexed {1} with texture source indexed {2}.", "normal", prevIndex, newIndex));
				}

				if (mat.OcclusionTexture != null &&
					mapEntry.source != mapEntry.newSource &&
					(mapEntry.duplicates.Any(dupe => dupe == mat.OcclusionTexture.Index.Id) || mapEntry.source == mat.OcclusionTexture.Index.Id))
				{
					var prevIndex = mat.OcclusionTexture.Index.Id;
					var newIndex = mapEntry.newSource;
					mat.OcclusionTexture.Index.Id = newIndex;
					Logging.Log(string.Format("Replaced {0} texture source indexed {1} with texture source indexed {2}.", "occlusion", prevIndex, newIndex));
				}

				if (mat.PbrMetallicRoughness.BaseColorTexture != null &&
					mapEntry.source != mapEntry.newSource &&
					(mapEntry.duplicates.Any(dupe => dupe == mat.PbrMetallicRoughness.BaseColorTexture.Index.Id) || mapEntry.source == mat.PbrMetallicRoughness.BaseColorTexture.Index.Id))
				{
					var prevIndex = mat.PbrMetallicRoughness.BaseColorTexture.Index.Id;
					var newIndex = mapEntry.newSource;
					mat.PbrMetallicRoughness.BaseColorTexture.Index.Id = newIndex;
					Logging.Log(string.Format("Replaced {0} texture source indexed {1} with texture source indexed {2}.", "diffuse", prevIndex, newIndex));
				}

				if (mat.PbrMetallicRoughness.MetallicRoughnessTexture != null &&
					mapEntry.source != mapEntry.newSource &&
					(mapEntry.duplicates.Any(dupe => dupe == mat.PbrMetallicRoughness.MetallicRoughnessTexture.Index.Id) || mapEntry.source == mat.PbrMetallicRoughness.MetallicRoughnessTexture.Index.Id))
				{
					var prevIndex = mat.PbrMetallicRoughness.MetallicRoughnessTexture.Index.Id;
					var newIndex = mapEntry.newSource;
					mat.PbrMetallicRoughness.MetallicRoughnessTexture.Index.Id = newIndex;
					Logging.Log(string.Format("Replaced {0} texture source indexed {1} with texture source indexed {2}.", "metallic/roughness", prevIndex, newIndex));
				}

				if (mat.Extensions?.ContainsKey(MOZ_lightmapExtensionFactory.EXTENSION_NAME) == true &&
					mapEntry.source != mapEntry.newSource &&
					(mapEntry.duplicates.Any(dupe => dupe == ((MOZ_lightmapExtension)mat.Extensions[MOZ_lightmapExtensionFactory.EXTENSION_NAME]).LightmapInfo.Index.Id) ||
					mapEntry.source == ((MOZ_lightmapExtension)mat.Extensions[MOZ_lightmapExtensionFactory.EXTENSION_NAME]).LightmapInfo.Index.Id))
				{
					var prevIndex = ((MOZ_lightmapExtension)mat.Extensions[MOZ_lightmapExtensionFactory.EXTENSION_NAME]).LightmapInfo.Index.Id;
					var newIndex = mapEntry.newSource;
					((MOZ_lightmapExtension)mat.Extensions[MOZ_lightmapExtensionFactory.EXTENSION_NAME]).LightmapInfo.Index.Id = newIndex;
					Logging.Log(string.Format("Replaced {0} texture source indexed {1} with texture source indexed {2}.", "Hubs lightmap", prevIndex, newIndex));
				}
			});
		}*/


		List<GLTFImage> imagesToAvoid = new List<GLTFImage>();
		List<GLTFTexture> texturesToAvoid = new List<GLTFTexture>();
		/*if (!convertLightmaps)
		{
			foreach (var mat in glbRoot.Materials)
			{
				// foreach (var texture in glbRoot.Textures)
				// {
				if (mat.Extensions != null && mat.Extensions.ContainsKey(MOZ_lightmapExtensionFactory.EXTENSION_NAME))
				{
					var lightmapIndex = ((MOZ_lightmapExtension)mat.Extensions[MOZ_lightmapExtensionFactory.EXTENSION_NAME]).LightmapInfo.Index.Id; // Refers to index of texture
					var lightmapTex = glbRoot.Textures.FirstOrDefault(tex => lightmapIndex == glbRoot.Textures.IndexOf(tex)); // The texture 
																															  // var lightmapTex = glbRoot.Textures[lightmapIndex];
																															  // Logging.Log(lightmapIndex + " : " + lightmapTex?.Source.Id + " : " + glbRoot.Textures.IndexOf(lightmapTex));
					if (lightmapTex != null)
					{
						// if (lightmapTex.Source.Id == tex.Source.Id)
						// {
						var imageIndex = lightmapTex.Source.Id;
						texturesToAvoid.Add(lightmapTex);
						if (imagesToAvoid.Contains(glbRoot.Images[imageIndex])) continue;
						imagesToAvoid.Add(glbRoot.Images[imageIndex]);
						if (!useExistingBasis)
						{
							Logging.Log("Skipping lightmap: " + glbRoot.Images[imageIndex].Uri);
							completedOperations += 5;
						}
						// }
					}
				}
				// }
			}
		}*/

		foreach (var texInst in TextureList.instance.textureList)
		{
			var img = glbRoot.Images.FirstOrDefault(image => glbRoot.Images.IndexOf(image) == texInst.imageIndex && !texInst.toggled);
			if (img != null)
				imagesToAvoid.Add(img);

			var texes = glbRoot.Textures.Where(texture => texture.Source.Id == texInst.imageIndex && !texInst.toggled);
			foreach (var tex in texes)
				texturesToAvoid.Add(tex);
		}

		List<GLTFImage> imagesToConvert = glbRoot.Images.Except(imagesToAvoid).ToList();
		List<GLTFTexture> texturesToConvert = glbRoot.Textures.Except(texturesToAvoid).ToList();



		var tasks = new List<Task>();
		SemaphoreSlim maxThread = new SemaphoreSlim(4);
		var tempPath = Path.Combine(pdp, "Temp");

		foreach (var image in imagesToConvert)
		{
			var nonBasisPath = Path.Combine(directoryPath, image.Uri);
			var filenameWith = Path.GetFileName(nonBasisPath);
			var filenameWithout = Path.GetFileNameWithoutExtension(nonBasisPath);
			var nonBasisDirectory = nonBasisPath.Replace(filenameWith, "");
			var basisPath = Path.Combine(nonBasisDirectory, filenameWithout + ".basis");

			var doesExist = File.Exists(basisPath);
			print(basisPath + " | " + doesExist);
			if (!useExistingBasis || !doesExist)
			{
				var newFilePath = Path.Combine(tempPath, Path.GetFileNameWithoutExtension(image.Uri) + ".png");
				if (image.MimeType != "image/png")
				{
					if (!Directory.Exists(tempPath))
						Directory.CreateDirectory(tempPath);
					using (var file = SixLabors.ImageSharp.Image.Load(Path.Combine(directoryPath, image.Uri)))
					{
						using (var newFile = File.Create(newFilePath))
						{
							file.SaveAsPng(newFile);
						}
					}
				}

				token.ThrowIfCancellationRequested();

#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
				var exe = "/bin/bash";
#else
				var exe = Path.Combine(Application.streamingAssetsPath, "basisu.exe");
#endif
				var uriSplit = image.Uri.Split(new char[] { '\\', '/' }).ToList();
				var fileName = uriSplit[uriSplit.Count - 1];
				var outputDir = Path.Combine(directoryPath, image.Uri.Substring(0, image.Uri.Length - fileName.Length));
				var filePath = "";
				if (image.MimeType != "image/png")
					filePath = newFilePath;
				else
					filePath = Path.Combine(directoryPath, image.Uri);

				var input = preserveAlpha ? " -alpha_file " : " -file ";

				var qualityArg = quality == 255 ? "-max_endpoints 16128 -max_selectors 16128" : "-q " + quality.ToString();

				var argString = qualityArg + " -comp_level " + level.ToString() + " -output_path \"" + outputDir + "\" -file \"" + filePath + "\" -selector_rdo_thresh " + threshold.ToString("0.00") + " -endpoint_rdo_thresh " + threshold.ToString("0.00") + " -mipmap";

#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
				var args = "-c './basisu " + argString + "'";
#else
				var args = argString;
#endif

				// var taskTime = new Stopwatch();
				// taskTime.Start();
				var now = DateTime.Now;
				var originalUri = image.Uri;

				if (useMultithreading)
				{
					await maxThread.WaitAsync();

					tasks.Add(Task.Factory.StartNew(async () =>
					{
						try
						{
							msgQueue.Add(new TaskData { msg = "Converting image '" + originalUri + "' to BASIS..." });
							await RunProcessAsync(exe, args);
							// taskTime.Stop();
							var ttc = DateTime.Now.Subtract(now).TotalSeconds;
							completedOperations += 5;
							msgQueue.Add(new TaskData { time = Mathf.RoundToInt((float)ttc * 100f) / 100f, name = fileName });
						}
						finally
						{
							maxThread.Release();
						}
					}).Unwrap());

				}
				else
				{
					Logging.Log("Converting image '" + originalUri + "' to BASIS...");
					await RunProcessAsync(exe, args);
					// taskTime.Stop();
					var ttc = DateTime.Now.Subtract(now).TotalSeconds;
					completedOperations += 5;
					Logging.Log("Finished in " + Mathf.RoundToInt((float)ttc * 100f) / 100f + " seconds.\n");
				}
			}

			var ext = image.MimeType.ToLower().Replace("image/", "");
			image.MimeType = "image/basis";
			Regex rgx = new Regex(@"\.(?:.(?!\.))+$");
			image.Uri = image.Uri.Replace(rgx.Match(image.Uri).Value, ".basis");

		}

		// if (!useExistingBasis)
		await Task.WhenAll(tasks.ToArray());

		msgQueue = new List<TaskData>();
		queueIndex = 0;

		if (Directory.Exists(tempPath))
			Directory.Delete(tempPath, true);


		foreach (var texture in texturesToConvert)
		{
			var moz = new MozHubsTextureBasisExtension(texture.Source ?? new ImageId());

			if (texture.Extensions == null)
				texture.Extensions = new Dictionary<string, IExtension>();

			if (!texture.Extensions.ContainsKey(MozHubsTextureBasisExtensionFactory.EXTENSION_NAME))
				texture.Extensions.Add(MozHubsTextureBasisExtensionFactory.EXTENSION_NAME, moz);
			texture.Source = null;

			if (texture.Sampler == null)
			{
				texture.Sampler = new SamplerId();
				texture.Sampler.Id = 0;
			}
		}

		if (texturesToConvert.Count > 0 || imagesToConvert.Count > 0)
		{
			if (glbRoot.ExtensionsUsed == null)
				glbRoot.ExtensionsUsed = new List<string>();

			if (glbRoot.ExtensionsRequired == null)
				glbRoot.ExtensionsRequired = new List<string>();

			if (!glbRoot.ExtensionsUsed.Contains(MozHubsTextureBasisExtensionFactory.EXTENSION_NAME))
				glbRoot.ExtensionsUsed.Add(MozHubsTextureBasisExtensionFactory.EXTENSION_NAME);

			if (!glbRoot.ExtensionsRequired.Contains(MozHubsTextureBasisExtensionFactory.EXTENSION_NAME))
				glbRoot.ExtensionsRequired.Add(MozHubsTextureBasisExtensionFactory.EXTENSION_NAME);
		}
	}

	private async Task CompleteTasks(Task[] tasks)
	{
		await Task.WhenAll(tasks);
	}

	private void ExportJson()
	{

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

	private void PrintJson()
	{
		var container = new Container();
		container.textures = gltfRoot.Textures;
		container.samplers = gltfRoot.Samplers;
		container.images = gltfRoot.Images;

		StringBuilder stringBuilder = new StringBuilder();
		StringWriter stringWriter = new StringWriter(stringBuilder);

		using (var writer = new JsonTextWriter(stringWriter))
		{
			writer.Formatting = Formatting.Indented;
			writer.WriteStartObject();

			if (gltfRoot.ExtensionsRequired != null)
			{
				writer.WritePropertyName("extensionsRequired");
				writer.WriteStartArray();
				foreach (var ext in gltfRoot.ExtensionsRequired)
				{
					writer.WriteValue(ext);
				}
				writer.WriteEndArray();
			}

			if (gltfRoot.ExtensionsUsed != null)
			{
				writer.WritePropertyName("extensionsUsed");
				writer.WriteStartArray();
				foreach (var ext in gltfRoot.ExtensionsUsed)
				{
					writer.WriteValue(ext);
				}
				writer.WriteEndArray();
			}

			if (container.textures != null && container.textures.Count > 0)
			{
				writer.WritePropertyName("textures");
				writer.WriteStartArray();
				foreach (var texture in container.textures)
				{
					writer.WriteStartObject();
					writer.WriteProperty(texture.Name, "", "name");
					writer.WriteProperty(texture.Source?.Id, "", "source");
					writer.WriteProperty(texture.Sampler?.Id, null, "sampler");
					writer.WriteExtensions(texture.Extensions);
					writer.WriteEndObject();
				}
				writer.WriteEndArray();
			}

			if (container.samplers != null && container.samplers.Count > 0)
			{
				writer.WritePropertyName("samplers");
				writer.WriteStartArray();
				foreach (var sampler in container.samplers)
				{
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

			if (container.images != null && container.images.Count > 0)
			{
				writer.WritePropertyName("images");
				writer.WriteStartArray();
				foreach (var image in container.images)
				{
					writer.WriteStartObject();
					writer.WriteProperty(image.Name, "", "name");
					writer.WriteProperty(image.Uri, "", "uri");
					writer.WriteProperty(image.MimeType, "", "mimeType");
					writer.WriteExtensions(image.Extensions);
					writer.WriteEndObject();
				}
				writer.WriteEndArray();
			}

			if (gltfRoot.Buffers != null && gltfRoot.Buffers.Count > 0)
			{
				writer.WritePropertyName("buffers");
				writer.WriteStartArray();
				foreach (var buffer in gltfRoot.Buffers)
				{
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

	private void OnApplicationQuit()
	{
		foreach (var process in processes)
		{
			if (process != null)
			{
				try { process.Kill(); } catch { }
			}
		}
	}

	private Task<int> RunProcessAsync(string fileName, string arguments)
	{

		var tcs = new TaskCompletionSource<int>();

		var process = new Process
		{
			StartInfo = {
				FileName = fileName,
				Arguments = arguments,
				WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
				WorkingDirectory = Application.streamingAssetsPath,
                #if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
                UseShellExecute = false
                #endif
            },
			EnableRaisingEvents = true
		};

		// #if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
		// process.StartInfo.UseShellExecute = false;
		// #endif

		try
		{
			processes.Add(process);
		}
		catch { }


		process.Exited += (sender, args) =>
		{
			tcs.SetResult(process.ExitCode);
			process.Dispose();
			processes.Remove(process);
			process = null;
		};

		process.Start();

		// UnityEngine.Debug.Log(process.StandardOutput.ReadToEnd());
		// UnityEngine.Debug.Log(process.StandardError.ReadToEnd());

		return tcs.Task;
	}

	private void TaskCompleted(object sender, TaskCompleteArgs e)
	{
		Logging.Log("Finished " + e.msg + " in " + Mathf.RoundToInt((Time.time - e.startTime) * 100f) / 100f + " seconds.\n");
	}
}

public class TaskCompleteArgs : EventArgs
{
	public string msg;
	public float startTime;
}

public static class Extensions
{
	public static void WriteProperty(this JsonTextWriter writer, object property, object compare, string name)
	{
		if (property != compare && property != null)
		{
			writer.WritePropertyName(name);
			writer.WriteValue(property);
		}
	}

	public static void WriteExtensions(this JsonTextWriter writer, Dictionary<string, IExtension> extensions)
	{
		if (extensions != null)
		{
			writer.WritePropertyName("extensions");
			writer.WriteStartObject();
			foreach (var extension in extensions)
			{
				writer.WritePropertyName(extension.Key);
				foreach (var jprop in extension.Value.Serialize())
					jprop.WriteTo(writer);
			}
			writer.WriteEndObject();
		}
	}

	public static bool EqualsTexture(this GLTFTexture texture1, GLTFTexture texture2)
	{
		var equal = (texture1.Extensions?.Keys == texture2.Extensions?.Keys &&
					texture1.Extensions?.Values == texture2.Extensions?.Values &&
					texture1.Name == texture2.Name &&
					texture1.Sampler?.Id == texture2.Sampler?.Id &&
					texture1.Source?.Id == texture2.Source?.Id);

		// Logging.Log(equal.ToString());
		return equal;
	}

	/// <summary>
	/// Wraps this object instance into an IEnumerable&lt;T&gt;
	/// consisting of a single item.
	/// </summary>
	/// <typeparam name="T"> Type of the object. </typeparam>
	/// <param name="item"> The instance that will be wrapped. </param>
	/// <returns> An IEnumerable&lt;T&gt; consisting of a single item. </returns>
	public static IEnumerable<T> Yield<T>(this T item)
	{
		yield return item;
	}
}