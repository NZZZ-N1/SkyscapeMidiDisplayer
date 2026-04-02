using System.Collections.Generic;
using System.Linq;

namespace EasySaving
{
	/// <summary>
	/// Used for saving or loading temporarily
	/// You don't need to use it
	/// </summary>
	[System.Serializable]
	public sealed class SavingTemp
	{
		public SavingPair[] values { get; set; }

		public SavingPack ToSavingPack()
		{
			SavingPack p = new SavingPack();
			foreach (var i in values)
				p.Dic.Add(i.ID, i.Value);
			return p;
		}
		
		public SavingTemp() { }
		public SavingTemp(SavingPack pack) : this()
		{
			LinkedList<SavingPair> list = new LinkedList<SavingPair>();
			foreach (var i in pack.Dic)
				list.AddLast(new SavingPair(i.Key, i.Value));
			values = list.ToArray();
		}
	}
}