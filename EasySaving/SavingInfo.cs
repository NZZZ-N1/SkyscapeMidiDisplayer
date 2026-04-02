using System;
using System.IO;

namespace EasySaving
{
	/// <summary>
	/// A container used for recording information about where the file will be saved
	/// You must instantiate
	/// </summary>
	public sealed class SavingInfo
	{
		public static SavingInfo Instance { get; private set; } = null!;

		public SavingInfo(string folder, string ext, bool allowAutoCreateWhenPathUnexist = false)
		{
			if (Instance != null)
				throw new ArgumentException("SaveInfo instance already exists");
			Instance = this;
			string? error = null;
			
			if (folder == null)
			{
				error = "Folder is not legal";
				goto WRONG;
			}
			if (ext == null)
			{
				error = "Ext is not legal";
				goto WRONG;
			}
			
			Folder = folder;
			Ext = ext;
			
			if (allowAutoCreateWhenPathUnexist && !Directory.Exists(folder))
			{
				Directory.CreateDirectory(folder);
			}

			return;
			WRONG:
			Instance = null!;
			throw new ArgumentException(error);
		}

		public readonly string? Folder = null;
		public readonly string? Ext = null;
		
		public static string ExecuteAppDirection => AppDomain.CurrentDomain.BaseDirectory;
	}
}