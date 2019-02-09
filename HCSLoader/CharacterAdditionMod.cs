using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace HCSLoader
{
	public class CharacterAdditionMod
	{
		public string Name { get; set; }
		public string Description { get; set; }
		public int Age { get; set; }
		public string CupSize { get; set; }
		public int Weight { get; set; }
		public GirlSmokesType Smokes { get; set; }
		public GirlDrinksType Drinks { get; set; }
		public int TalentLevel { get; set; }
		public int StyleLevel { get; set; }

		public List<string> Fetishes { get; set; }

		public List<CharacterPart> Parts { get; set; }



		public string ModDirectory { get; set; }


		public static CharacterAdditionMod Load(string directory)
		{
			string girlManifestPath = Path.Combine(directory, "girl.json");

			var mod = JsonConvert.DeserializeObject<CharacterAdditionMod>(File.ReadAllText(girlManifestPath));

			mod.ModDirectory = directory;

			return mod;
		}
	}

	public class CharacterPart
	{
		public string File { get; set; }

		public string Type { get; set; }

		public int X { get; set; }

		public int Y { get; set; }
	}
}