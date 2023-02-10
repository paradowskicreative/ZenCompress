using System.Text.RegularExpressions;
using GLTF.Schema;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
// using System.Text.RegularExpressions;

public class HDRSwap
{
	public Dictionary<int, string> hdrMap = new Dictionary<int, string>();
	public Dictionary<int, IExtension> texMap = new Dictionary<int, IExtension>();
	public void SwapOut(GLTFRoot gltfRoot)
	{
		hdrMap.Clear();
		texMap.Clear();
		gltfRoot.Images.ForEach(image =>
		{
			if (image.Uri.EndsWith(".hdr"))
			{
				int index = gltfRoot.Images.IndexOf(image);
				string urimime = image.Uri + "|" + image.MimeType;
				hdrMap.Add(index, urimime);

				string newUri = Path.Combine(Application.streamingAssetsPath, "hdr.png");
				image.Uri = newUri;
				image.MimeType = "image/png";
			}
		});
		gltfRoot.Textures.ForEach(texture =>
		{
			if (texture.Extensions != null && texture.Extensions.ContainsKey("MOZ_texture_rgbe"))
			{
				int index = gltfRoot.Textures.IndexOf(texture);
				texMap.Add(index, texture.Extensions["MOZ_texture_rgbe"]);
				texture.Source = new ImageId();
				string source = (texture.Extensions["MOZ_texture_rgbe"] as DefaultExtension).ExtensionData.Value.ToString();
				Regex rx = new Regex(@"(?<=""source"":\s*)[0-9]+");
				Match match = rx.Match(source);
				texture.Source.Id = Int32.Parse(match.Value.ToString());
			}
		});
	}

	public void SwapIn(GLTFRoot gltfRoot)
	{
		foreach (KeyValuePair<int, string> entry in hdrMap)
		{
			GLTFImage image = gltfRoot.Images[entry.Key];
			string[] mimeUri = entry.Value.Split('|');
			image.Uri = mimeUri[0];
			image.MimeType = mimeUri[1];
		}
		foreach (KeyValuePair<int, IExtension> entry in texMap)
		{
			GLTFTexture texture = gltfRoot.Textures[entry.Key];
			texture.Source = null;
		}
	}
}