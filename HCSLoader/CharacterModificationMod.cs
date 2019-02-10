using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace HCSLoader
{
	public class CharacterModificationMod
	{
		public string CharacterName { get; set; }
		
		public Dictionary<int, CharacterPart> ReplacementOutfits { get; set; }


		public string ModDirectory { get; set; }


		public static bool TryLoad(string directory, out CharacterModificationMod mod)
		{
			string girlManifestPath = Path.Combine(directory, "editgirl.json");

			if (!File.Exists(girlManifestPath))
			{
				mod = null;
				return false;
			}

			var template = JsonConvert.DeserializeObject<SerializationTemplate>(File.ReadAllText(girlManifestPath));

			mod = new CharacterModificationMod
			{
				CharacterName = template.CharacterName,
				ReplacementOutfits = template.ReplacementOutfits.ToDictionary(x => int.Parse(x.Key), x => x.Value),
				ModDirectory = directory
			};

			return true;
		}

		private class SerializationTemplate
		{
			public string CharacterName { get; set; }

			public Dictionary<string, CharacterPart> ReplacementOutfits { get; set; }
		}
	}
}