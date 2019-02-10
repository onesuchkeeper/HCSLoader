using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Harmony;
using BepInEx.Logging;
using Harmony;
using UnityEngine;
using static HCSLoader.ResourceLoader;

namespace HCSLoader
{
	[BepInPlugin("com.bepis.hcsloader", "HCSLoader", "1.1")]
	public class HCSLoader : BaseUnityPlugin
	{
		public new static ManualLogSource Logger;

		public static List<CharacterAdditionMod> CharacterAdditions = new List<CharacterAdditionMod>();
		public static List<CharacterModificationMod> CharacterModifications = new List<CharacterModificationMod>();

		public HCSLoader()
		{
			Logger = base.Logger;

			HarmonyWrapper.PatchAll();

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
			
			Logger.Log(LogLevel.Message, $"Loaded {CharacterAdditions.Count + CharacterModifications.Count} mods.");
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

				var girlDefinition = ScriptableObject.CreateInstance<GirlDefinition>();

				girlDefinition.girlName = addition.Name;
				girlDefinition.girlDescription = addition.Description;
				girlDefinition.girlBustOffset = 0;
				girlDefinition.girlIcons = new[] { LoadSprite(Path.Combine(addition.ModDirectory, "icon.png")) };
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

				girlDefinition.dollParts = ProcessDollParts(addition, out var outfits).ToList();

				girlDefinition.hairstyles = new List<string> { "Default", "Default", "Default", "Default", "Default", };
				girlDefinition.outfits = outfits;

				girlDefinition.voiceRecruit = LoadAudioGroup(Path.Combine(addition.ModDirectory, "voice-recruit"));
				girlDefinition.voiceEmploy = LoadAudioGroup(Path.Combine(addition.ModDirectory, "voice-employ"));
				girlDefinition.voiceProfile = LoadAudioGroup(Path.Combine(addition.ModDirectory, "voice-profile"));
				girlDefinition.voiceStart = LoadAudioGroup(Path.Combine(addition.ModDirectory, "voice-start"));
				girlDefinition.voiceFinish = LoadAudioGroup(Path.Combine(addition.ModDirectory, "voice-finish"));
				girlDefinition.voiceBlocked = LoadAudioGroup(Path.Combine(addition.ModDirectory, "voice-blocked"));
				girlDefinition.voiceAccessory = LoadAudioGroup(Path.Combine(addition.ModDirectory, "voice-accessory"));

				girlDefinition.id = currentId;


				definitions.Add(currentId++, girlDefinition);
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

				if (modification.ReplacementOutfits != null)
					foreach (var kv in modification.ReplacementOutfits)
					{
						if (kv.Key < 1 || kv.Key > 5)
						{
							Logger.LogWarning($"({modification.CharacterName}) Invalid outfit index '{kv.Key}', skipping");
							continue;
						}

						if (kv.Value.Name != null)
							girl.outfits[kv.Key - 1] = kv.Value.Name;

						var dollPart = new GirlDefinitionDollPart
						{
							editorExpanded = true,
							sprite = LoadSprite(Path.Combine(modification.ModDirectory, kv.Value.File)),
							type = GirlDollPartType.OUTFIT,
							x = kv.Value.X,
							y = kv.Value.Y
						};

						int? index = girl.dollParts.FindNthIndex(kv.Key, x => x.type == GirlDollPartType.OUTFIT);

						if (!index.HasValue)
						{
							Logger.LogWarning($"({modification.CharacterName}) Outfit {kv.Key} was not found, skipping");
							continue;
						}

						girl.dollParts[index.Value] = dollPart;
					}
			}

			Logger.Log(LogLevel.Info, $"{CharacterModifications.Count} modifications loaded");
		}

		protected static List<GirlDefinitionDollPart> ProcessDollParts(CharacterAdditionMod addition, out List<string> outfits)
		{
			List<GirlDefinitionDollPart> parts = new List<GirlDefinitionDollPart>(addition.Parts.Count);
			outfits = new List<string>(5);

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

				if (dollPart.type == GirlDollPartType.OUTFIT)
				{
					outfits.Add(part.Name ?? $"Outfit {outfits.Count + 1}");
				}
			}

			while (outfits.Count < 5)
				outfits.Add("Default");

			return parts;
		}

		#region Hooks

		private static bool _initialized = false;

		[HarmonyPostfix, HarmonyPatch(typeof(Game), MethodType.Constructor, new Type[] { })]
		static void LoadGirlsHook()
		{
			if (_initialized)
				return;

			PerformLoad();

			_initialized = true;
		}

		#endregion
	}
}