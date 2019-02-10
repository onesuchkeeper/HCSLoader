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


		public static bool TryLoad(string directory, out CharacterAdditionMod mod)
		{
			string girlManifestPath = Path.Combine(directory, "addgirl.json");

			if (!File.Exists(girlManifestPath))
			{
				mod = null;
				return false;
			}

			mod = JsonConvert.DeserializeObject<CharacterAdditionMod>(File.ReadAllText(girlManifestPath));

			mod.ModDirectory = directory;

			return true;
		}
	}
}