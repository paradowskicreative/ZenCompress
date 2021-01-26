using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TextureList : MonoBehaviour
{
	public List<TextureInstance> textureList = new List<TextureInstance>();
	public GameObject textureEntryPrefab;
	public static TextureList instance;
	public enum MapType
	{
		DIFFUSE,
		NORMAL,
		LIGHTMAP,
		METALROUGH,
		EMISSIVE,
		OCCLUSION
	}
	public struct Image
	{
		public string name;
		public string URI;
		public int imageIndex;
		public MapType mapType;
		public bool toggled;
	}

	void Awake()
	{
		instance = this;
	}

	public void PopulateFromImages(List<Image> images)
	{

		for (int i = textureList.Count; i > 0; i--)
		{
			var go = textureList[i - 1];
			textureList.RemoveAt(i - 1);
			Destroy(go);
		}

		for (int i = 0; i < images.Count; i++)
		{
			var entry = Instantiate(textureEntryPrefab) as GameObject;
			var textureInstance = entry.GetComponent<TextureInstance>();

			textureInstance.mapType = images[i].mapType;
			textureInstance.name = images[i].name;
			textureInstance.imageIndex = images[i].imageIndex;
			textureInstance.URI = images[i].URI;
			textureInstance.toggled = images[i].toggled;


			textureList.Add(entry.GetComponent<TextureInstance>());
			entry.transform.parent = this.transform;

			textureInstance.Setup();


			// entry.AddComponent<LayoutElement>();
		}

		// LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)this.transform);
	}

	public void ToggleGroups(int toggle)
	{
		var toggleGroup = (MapType)toggle;


	}
}
