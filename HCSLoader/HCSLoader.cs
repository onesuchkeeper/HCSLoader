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
	[BepInPlugin("com.bepis.hcsloader", "HCSLoader", "1.0")]
	public class HCSLoader : BaseUnityPlugin
	{
		public new static ManualLogSource Logger;

		public static List<CharacterAdditionMod> CharacterAdditions = new List<CharacterAdditionMod>();

		public HCSLoader()
		{
			Logger = base.Logger;

			HarmonyWrapper.PatchAll();

			string modsDirectory = Path.Combine(Paths.GameRootPath, "Mods");

			if (!Directory.Exists(modsDirectory))
				Directory.CreateDirectory(modsDirectory);

			foreach (var subdir in Directory.GetDirectories(modsDirectory, "*", SearchOption.TopDirectoryOnly))
			{
				var mod = CharacterAdditionMod.Load(subdir);

				CharacterAdditions.Add(mod);
			}
			
			Logger.Log(LogLevel.Message, $"Loaded {CharacterAdditions.Count} mods.");
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

				girlDefinition.dollParts = ProcessDollParts(addition).ToList();

				girlDefinition.hairstyles = new List<string> { "Default", "Default", "Default", "Default", "Default", };
				girlDefinition.outfits = new List<string> { "Default", "Default", "Default", "Default", "Default", };

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
		}

		protected static IEnumerable<GirlDefinitionDollPart> ProcessDollParts(CharacterAdditionMod addition)
		{
			foreach (var part in addition.Parts)
			{
				yield return new GirlDefinitionDollPart
				{
					editorExpanded = true,
					sprite = LoadSprite(Path.Combine(addition.ModDirectory, part.File)),
					type = (GirlDollPartType)Enum.Parse(typeof(GirlDollPartType), part.Type, true),
					x = part.X,
					y = part.Y
				};
			}
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