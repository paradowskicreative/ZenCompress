﻿using System.IO;
using UnityEngine;

namespace UnityGLTF.Examples
{
	public class GLTFExporterTest : MonoBehaviour
	{
		public string RetrieveTexturePath(UnityEngine.Texture texture)
		{
			return texture.name;
		}

		// Use this for initialization
		void Awake()
		{
			var exporter = new GLTFSceneExporter(new[] {transform}, RetrieveTexturePath);
			var appPath = Application.dataPath;
			var wwwPath = appPath.Substring(0, appPath.LastIndexOf("Assets")) + "www";
			exporter.SaveGLTFandBin(Path.Combine(wwwPath, "TestScene"), "TestScene");
		}
	}
}
