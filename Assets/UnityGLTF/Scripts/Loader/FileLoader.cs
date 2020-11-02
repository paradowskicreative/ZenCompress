using System.IO;
using System;
using System.Threading.Tasks;
using UnityEngine;

namespace UnityGLTF.Loader
{
	public class FileLoader : IDataLoader, IDataLoader2
	{
		private readonly string _rootDirectoryPath;
		public Stream thisStream;
		private string filePath;
		public string pdp;

		public FileLoader(string rootDirectoryPath)
		{
			_rootDirectoryPath = rootDirectoryPath;
			pdp = ImportExport.pdp;
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

			// using(FileStream stream = File.OpenRead(pathToLoad)) {
			// 	// stream.Read()
			// 	// var fileName = Path.GetFileName(pathToLoad);
			// 	// filePath = Path.Combine(pdp, fileName);
			// 	// if(File.Exists(filePath))
			// 	// 	File.Delete(filePath);
			// 	// thisStream = File.Create(filePath);
			// 	// thisStream.Position = 0;
			// 	// stream.Position = 0;
			// 	thisStream = new MemoryStream();
			// 	stream.CopyTo(thisStream);
			// }

			thisStream = File.OpenRead(pathToLoad);
			return thisStream;
		}

		public void CloseStream() {
			if(thisStream != null)
				thisStream.Dispose();
			// if(File.Exists(filePath))
			// 	File.Delete(filePath);
		}
	}
}
