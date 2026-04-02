using System;
using System.Collections.Generic;

namespace EasySaving
{
	/// <summary>
	/// Use it to save what you want.
	/// Add and Get by accessing Dic
	/// </summary>
	public class SavingPack
	{
		public Dictionary<string, object> Dic { get; private set; } = new Dictionary<string, object>();

		public void Add(string key, object value)
		{
			if (Dic.ContainsKey(key))
				Dic[key] = value;
			else
				Dic.Add(key, value);
		}
		public bool Remove(string key) => Dic.Remove(key);

		public T TryGetValue<T>(string id, T defaultValue)
		{
			if (!Dic.TryGetValue(id, out var ov))
				return defaultValue;
			return (T)ov;
		}
		public bool GetValueUnsafely<T>(string id, out object ov, T defaultValue)
		{
			try
			{
				T v = TryGetValue<T>(id, defaultValue);
				ov = v;
				return true;
			}
			catch
			{
				ov = defaultValue;
				return false;
			}
		}
	}
}