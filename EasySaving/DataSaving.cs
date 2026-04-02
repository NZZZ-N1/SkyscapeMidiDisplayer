using System;
using System.IO;
using System.Xml.Serialization;

namespace EasySaving
{
	/// <summary>
	///	The main class used for saving or loading data
	/// You can obtain a SavingPack here,And use the SavingData to get or add the value you want to
	/// </summary>
	public static class DataSaving
	{
		private static SavingInfo info => SavingInfo.Instance;

		/// <summary>
		/// 检查各数据是否有效
		/// </summary>
		public static void CheckValidCall()
		{
			if (info == null)
				throw new InfoInstanceMissingException();
		}

		/// <summary>
		/// 通过相对路径获取文件的绝对路径
		/// 允许使用folder1/folder2/file的格式
		/// </summary>
		public static string GetFilePath(string relativeFilePath)
		{
			CheckValidCall();
			if (relativeFilePath == null)
				throw new ArgumentException();
			string[] strs = relativeFilePath.Split('/');
			string path = info.Folder;
			for (int i = 0; i < strs.Length; i++)
				path = Path.Combine(path, strs[i]);

			string ext = Path.GetExtension(path);
            if (string.IsNullOrEmpty(ext))
				path = Path.ChangeExtension(path, info.Ext);
            return path;
        }

		/// <summary>
		/// 检查文件是否存在
		/// </summary>
		public static bool FileExists(string fileName)
		{
			CheckValidCall();
			return File.Exists(GetFilePath(fileName));
		}

		/// <summary>
		/// 保存文件
		/// </summary>
		public static void Save(SavingPack pack, string relativeFilePath)
		{
			CheckValidCall();

			string path = GetFilePath(relativeFilePath);

			var dir = Path.GetDirectoryName(path);
			if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
				Directory.CreateDirectory(dir);

			SavingTemp t = new SavingTemp(pack);
			var serializer = new XmlSerializer(typeof(SavingTemp));
			var writer = new StreamWriter(path);
			serializer.Serialize(writer, t);
			writer.Close();
		}

		/// <summary>
		/// 读取文件
		/// </summary>
		public static SavingPack Load(string relativeFilePath, bool returnNullWhenNotExist = false)
		{
			CheckValidCall();

			try
			{
				string path = GetFilePath(relativeFilePath);
				if (!File.Exists(path))
					throw new DirectoryNotFoundException();
				
				var serializer = new XmlSerializer(typeof(SavingTemp));
				var reader = new StreamReader(path);
				SavingTemp t = (SavingTemp)serializer.Deserialize(reader);
				reader.Close();

				return t.ToSavingPack();
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
				if (returnNullWhenNotExist)
					return null;
				return new SavingPack();
			}
		}
	}
}