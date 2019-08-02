using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Harmony;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using static HCSLoader.ResourceLoader;

namespace HCSLoader
{
	[BepInPlugin("com.bepis.hcsloader", "HCSLoader", "1.2")]
	public class HCSLoader : BaseUnityPlugin
	{
		public new static ManualLogSource Logger;

		public static List<CharacterAdditionMod> CharacterAdditions = new List<CharacterAdditionMod>();
		public static List<GirlDefinition> CharacterAdditionDefs = new List<GirlDefinition>();
		public static List<CharacterModificationMod> CharacterModifications = new List<CharacterModificationMod>();

		public const int MaxOutfits = 5;

		public HCSLoader()
		{
			Logger = base.Logger;

			var harmonyInstance = HarmonyWrapper.PatchAll();
			harmonyInstance.Patch(AccessTools.Constructor(typeof(Game), Type.EmptyTypes), postfix: new HarmonyMethod(typeof(HCSLoader), nameof(LoadGirlsHook)));
			
			string modsDirectory = Path.Combine(Paths.GameRootPath, "Mods");

			if (!Directory.Exists(modsDirectory))
				Directory.CreateDirectory(modsDirectory);

			foreach (var subdir in Directory.GetDirectories(modsDirectory, "*", SearchOption.TopDirectoryOnly))
			{
				if (CharacterAdditionMod.TryLoad(subdir, out var addMod))
				{
					CharacterAdditions.Add(addMod);
				}
				else if (CharacterModificationMod.TryLoad(subdir, out var editMod))
				{
					CharacterModifications.Add(editMod);
				}
				else
				{
					Logger.LogWarning($"Unknown mod type for folder '{Path.GetFileName(subdir)}', skipping");
				}
			}

			Logger.Log(LogLevel.Message, $"Found {CharacterAdditions.Count + CharacterModifications.Count} mods.");
		}

		public void Update()
		{
			if (Game.Manager.State == GameState.INITING)
				return;

			GirlsWindowPaging.Update();
			WardrobeWindowPaging.Update();
		}

		public static void PerformLoad()
		{
			//load additions

			Dictionary<int, GirlDefinition> definitions = (Dictionary<int, GirlDefinition>)AccessTools.Field(typeof(GirlData), "_definitions").GetValue(Game.Data.Girls);
			int currentId = 19;

			foreach (var addition in CharacterAdditions)
			{
				Logger.LogInfo($"Loading girl '{addition.Name}'");

				//validation

				if (!File.Exists(Path.Combine(addition.ModDirectory, "photo.png")))
				{
					Logger.LogError($"Could not find character photo at '{Path.Combine(addition.ModDirectory, "photo.png")}', skipping");
					continue;
				}

				if (!File.Exists(Path.Combine(addition.ModDirectory, "photothumbnail.png")))
				{
					Logger.LogError($"Could not find character photo thumbnail at '{Path.Combine(addition.ModDirectory, "photothumbnail.png")}', skipping");
					continue;
				}

				if (!File.Exists(Path.Combine(addition.ModDirectory, "icon1.png")))
				{
					Logger.LogError($"Could not find icon #1 at '{Path.Combine(addition.ModDirectory, "icon1.png")}', skipping");
					continue;
				}


				var girlDefinition = ScriptableObject.CreateInstance<GirlDefinition>();

				girlDefinition.girlName = addition.Name;
				girlDefinition.girlDescription = addition.Description;
				girlDefinition.girlBustOffset = 0;
				girlDefinition.girlIcons = LoadIconSprites(addition.ModDirectory, true).OrderBy(x => x.Key).Select(x => x.Value).ToArray();
				girlDefinition.girlPhotoThumbnail = LoadSprite(Path.Combine(addition.ModDirectory, "photothumbnail.png"));
				girlDefinition.girlPromo = LoadSprite(Path.Combine(addition.ModDirectory, "photo.png"));
				girlDefinition.promoIsLewd = false;
				girlDefinition.age = addition.Age;
				girlDefinition.race = default(GirlRaceType);
				girlDefinition.personality = default(GirlPersonalityType);
				girlDefinition.cupSize = (GirlCupSizeType)Enum.Parse(typeof(GirlCupSizeType), addition.CupSize, true);
				girlDefinition.weight = addition.Weight;
				girlDefinition.smokes = addition.Smokes;
				girlDefinition.drinks = addition.Drinks;
				girlDefinition.IsHuniePopGirl = false;
				girlDefinition.startTalentLevel = addition.TalentLevel;
				girlDefinition.startStyleLevel = addition.StyleLevel;
				girlDefinition.holdUntilEmployeeCount = 1;

				girlDefinition.fetishes = addition.Fetishes
												  .Select(x => Game.Data.Fetishes.GetAll()
																   .Find(y => y.fetishName.Equals(x, StringComparison.OrdinalIgnoreCase)))
												  .ToList();

				girlDefinition.dollParts = ProcessDollParts(addition, out girlDefinition.outfits, out girlDefinition.hairstyles);

				girlDefinition.voiceRecruit = LoadAudioGroup(Path.Combine(addition.ModDirectory, "voice-recruit"));
				girlDefinition.voiceEmploy = LoadAudioGroup(Path.Combine(addition.ModDirectory, "voice-employ"));
				girlDefinition.voiceProfile = LoadAudioGroup(Path.Combine(addition.ModDirectory, "voice-profile"));
				girlDefinition.voiceStart = LoadAudioGroup(Path.Combine(addition.ModDirectory, "voice-start"));
				girlDefinition.voiceFinish = LoadAudioGroup(Path.Combine(addition.ModDirectory, "voice-finish"));
				girlDefinition.voiceBlocked = LoadAudioGroup(Path.Combine(addition.ModDirectory, "voice-blocked"));
				girlDefinition.voiceAccessory = LoadAudioGroup(Path.Combine(addition.ModDirectory, "voice-accessory"));

				girlDefinition.id = currentId;


				definitions.Add(currentId++, girlDefinition);
				CharacterAdditionDefs.Add(girlDefinition);
			}

			AccessTools.Field(typeof(GirlData), "_highestId").SetValue(Game.Data.Girls, currentId);

			Logger.Log(LogLevel.Info, $"{Game.Data.Girls.GetAll().Count} girls loaded");
			

			//load modifications

			var girlDict = Game.Data.Girls.GetAll().ToDictionary(x => x.girlName, x => x, StringComparer.OrdinalIgnoreCase);

			foreach (var modification in CharacterModifications)
			{
				if (!girlDict.TryGetValue(modification.CharacterName, out var girl))
				{
					Logger.LogWarning($"Unable to find girl '{modification.CharacterName}', skipping");
					continue;
				}

				void ReplacePart(GirlDollPartType type, int inputIndex, CharacterPart part)
				{
					int? index = girl.dollParts.FindNthIndex(inputIndex, x => x.type == type);

					if (!index.HasValue)
					{
						Logger.LogWarning($"({modification.CharacterName}) {(type == GirlDollPartType.OUTFIT ? "Outfit" : "Hairstyle")} {inputIndex} was not found, skipping");
						return;
					}

					girl.dollParts[index.Value] = new GirlDefinitionDollPart
					{
						editorExpanded = true,
						sprite = LoadSprite(Path.Combine(modification.ModDirectory, part.File)),
						type = type,
						x = part.X,
						y = part.Y
					};
				}

				if (modification.ReplacementOutfits != null)
					foreach (var kv in modification.ReplacementOutfits)
					{
						if (kv.Key < 1 || kv.Key > MaxOutfits)
						{
							Logger.LogWarning($"({modification.CharacterName}) Invalid outfit index '{kv.Key}', skipping");
							continue;
						}

						if (kv.Value.Name != null)
							girl.outfits[kv.Key - 1] = kv.Value.Name;

						ReplacePart(GirlDollPartType.OUTFIT, kv.Key, kv.Value);
					}

				if (modification.ReplacementHairstyles != null)
					foreach (var kv in modification.ReplacementHairstyles)
					{
						if (kv.Key < 1 || kv.Key > MaxOutfits)
						{
							Logger.LogWarning($"({modification.CharacterName}) Invalid hairstyle index '{kv.Key}', skipping");
							continue;
						}

						if (kv.Value.Name != null)
							girl.hairstyles[kv.Key - 1] = kv.Value.Name;

						if (kv.Value.Front != null)
							ReplacePart(GirlDollPartType.FRONTHAIR, kv.Key, kv.Value.Front);

						if (kv.Value.Back != null)
							ReplacePart(GirlDollPartType.BACKHAIR, kv.Key, kv.Value.Back);

						if (kv.Value.Shadow != null)
							ReplacePart(GirlDollPartType.HAIRSHADOW, kv.Key, kv.Value.Shadow);
					}

				foreach (var kv in LoadIconSprites(modification.ModDirectory, false))
				{
					girl.girlIcons[kv.Key - 1] = kv.Value;
				}
			}

			Logger.Log(LogLevel.Info, $"{CharacterModifications.Count} modifications loaded");
		}

		protected static List<GirlDefinitionDollPart> ProcessDollParts(CharacterAdditionMod addition, out List<string> outfits, out List<string> hairstyles)
		{
			List<GirlDefinitionDollPart> parts = new List<GirlDefinitionDollPart>(addition.Parts.Count);
			outfits = new List<string>(MaxOutfits);
			hairstyles = new List<string>(MaxOutfits);

			foreach (var part in addition.Parts)
			{
				var dollPart = new GirlDefinitionDollPart
				{
					editorExpanded = true,
					sprite = LoadSprite(Path.Combine(addition.ModDirectory, part.File)),
					type = (GirlDollPartType)Enum.Parse(typeof(GirlDollPartType), part.Type, true),
					x = part.X,
					y = part.Y
				};

				parts.Add(dollPart);

				if (dollPart.type == GirlDollPartType.OUTFIT && outfits.Count < MaxOutfits)
				{
					outfits.Add(part.Name ?? $"Outfit {outfits.Count + 1}");
				}
				else if (dollPart.type == GirlDollPartType.FRONTHAIR && hairstyles.Count < MaxOutfits)
				{
					hairstyles.Add(part.Name ?? $"Hairstyle {outfits.Count + 1}");
				}
			}

			while (outfits.Count < MaxOutfits)
				outfits.Add("Default");

			while (hairstyles.Count < MaxOutfits)
				hairstyles.Add("Default");

			return parts;
		}

		protected static Dictionary<int, Sprite> LoadIconSprites(string directory, bool fillBlanks)
		{
			Dictionary<int, Sprite> sprites = new Dictionary<int, Sprite>();

			for (int i = 1; i < 6; i++)
			{
				string path = Path.Combine(directory, $"icon{i}.png");

				if (File.Exists(path))
				{
					sprites[i] = LoadSprite(path);
				}
				else if (fillBlanks)
				{
					if (i == 1)
						throw new Exception("Could not find icon #1");

					sprites[i] = sprites[1];
				}
			}

			return sprites;
		}

		#region Hooks

		private static bool _initialized = false;

		[HarmonyPostfix, HarmonyPatch(typeof(Game), MethodType.Constructor, new Type[] { })]
		public static void LoadGirlsHook()
		{
			if (_initialized)
				return;

			Logger.LogInfo("Performing mod load");

			PerformLoad();

			_initialized = true;
		}

		[HarmonyPostfix, HarmonyPatch(typeof(Game), nameof(Game.Init))]
		public static void GameInitHook()
		{
			// Add all custom characters to the wardrobe, if they aren't already there
			foreach (var girlDefinition in CharacterAdditionDefs)
				Game.Persistence.AddWardrobeGirl(girlDefinition);
		}
		
		#endregion
	}
}