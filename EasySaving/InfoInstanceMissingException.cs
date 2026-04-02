using System;

namespace EasySaving
{
	/// <summary>
	/// It will be thrown when you try using EasySaving without instantiating a SaveInfo
	/// </summary>
	public sealed class InfoInstanceMissingException : Exception
	{
		public override string Message => "You must instantiate a SaveInfo before using EasySaving";
	}
}