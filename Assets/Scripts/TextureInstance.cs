using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TextureInstance : MonoBehaviour
{
	public string name;
	public string URI;
	public int imageIndex;
	public bool toggled;
	public Toggle toggle;
	public TextMeshProUGUI label;
	public TextMeshProUGUI designation;
	public TextureList.MapType mapType;

	public void ChangeSelfToggle(bool isOn)
	{
		toggled = isOn;
	}

	public void Setup()
	{
		label.text = name;
		switch(mapType) {
			case TextureList.MapType.DIFFUSE:
				designation.text = "D";
				break;
			case TextureList.MapType.EMISSIVE:
				designation.text = "E";
				break;
			case TextureList.MapType.LIGHTMAP:
				designation.text = "L";
				break;
			case TextureList.MapType.METALROUGH:
				designation.text = "M/R";
				break;
			case TextureList.MapType.NORMAL:
				designation.text = "N";
				break;
			case TextureList.MapType.OCCLUSION:
				designation.text = "O";
				break;
			default:
				designation.text = "?";
				break;
		}
		toggle.isOn = toggled;
	}
}
