using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace HCSLoader
{
	public class CharacterModificationMod
	{
		public string CharacterName { get; set; }

		[JsonIgnore]
		public Dictionary<int, CharacterPart> ReplacementOutfits { get; set; }

		[JsonProperty("ReplacementOutfits")]
		private Dictionary<string, CharacterPart> internalReplacementOutfits { get; set; }


		public string ModDirectory { get; set; }


		public static bool TryLoad(string directory, out CharacterModificationMod mod)
		{
			string girlManifestPath = Path.Combine(directory, "editgirl.json");

			if (!File.Exists(girlManifestPath))
			{
				mod = null;
				return false;
			}
			
			mod = JsonConvert.DeserializeObject<CharacterModificationMod>(File.ReadAllText(girlManifestPath));

			mod.ReplacementOutfits = mod.internalReplacementOutfits.ToDictionary(x => int.Parse(x.Key), x => x.Value);
			mod.ModDirectory = directory;

			return true;
		}
	}
}