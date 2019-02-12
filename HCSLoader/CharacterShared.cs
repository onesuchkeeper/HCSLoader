namespace HCSLoader
{
	public class CharacterPart
	{
		public string File { get; set; }

		public string Type { get; set; }

		public int X { get; set; }

		public int Y { get; set; }

		public string Name { get; set; }
	}

	public class HairstylePart
	{
		public string Name { get; set; }

		public CharacterPart Front { get; set; }
		public CharacterPart Back { get; set; }
		public CharacterPart Shadow { get; set; }
	}
}