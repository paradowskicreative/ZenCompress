using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
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
			Destroy(go.gameObject);
		}

		// Order the list.
		var orderedImages = images.OrderBy(img => img.name).ToList();
		orderedImages = orderedImages.OrderBy(img => img.mapType).ToList();

		for (int i = 0; i < orderedImages.Count; i++)
		{
			var entry = Instantiate(textureEntryPrefab) as GameObject;
			var textureInstance = entry.GetComponent<TextureInstance>();

			textureInstance.mapType = orderedImages[i].mapType;
			textureInstance.name = orderedImages[i].name;
			textureInstance.imageIndex = orderedImages[i].imageIndex;
			textureInstance.URI = orderedImages[i].URI;
			textureInstance.toggled = orderedImages[i].toggled;


			textureList.Add(entry.GetComponent<TextureInstance>());
			entry.transform.parent = this.transform;
			entry.transform.localScale = Vector3.one;

			textureInstance.Setup();
		}

	}

	public void ToggleGroups(int toggle)
	{
		var toggleGroup = (MapType)toggle;


	}
}
