namespace EasySaving
{
	/// <summary>
	/// This struct would record the value which you want to save
	/// You don't need to use it,EasySaving will do All the things
	/// </summary>
	public struct SavingPair
	{
		public string ID;
		public object Value;

		public SavingPair(string id, object value)
		{
			ID = id;
			Value = value;
		}
	}
}