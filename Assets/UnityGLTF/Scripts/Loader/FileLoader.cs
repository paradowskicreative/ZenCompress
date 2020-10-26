﻿using System.IO;
using System;
using System.Threading.Tasks;

namespace UnityGLTF.Loader
{
	public class FileLoader : IDataLoader, IDataLoader2
	{
		private readonly string _rootDirectoryPath;
		public FileStream thisStream;

		public FileLoader(string rootDirectoryPath)
		{
			_rootDirectoryPath = rootDirectoryPath;
		}

		public Task<Stream> LoadStreamAsync(string relativeFilePath)
		{
			return Task.Run(() => LoadStream(relativeFilePath));
		}

		public Stream LoadStream(string relativeFilePath)
		{
			if (relativeFilePath == null)
			{
				throw new ArgumentNullException("relativeFilePath");
			}

			string pathToLoad = Path.Combine(_rootDirectoryPath, relativeFilePath);
			if (!File.Exists(pathToLoad))
			{
				throw new FileNotFoundException("Buffer file not found", relativeFilePath);
			}
			thisStream = File.OpenRead(pathToLoad);
			return thisStream;
		}

		public void CloseStream() {
			thisStream.Dispose();
		}
	}
}
