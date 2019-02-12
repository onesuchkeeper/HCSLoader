using System.Collections.Generic;
using System.Reflection;
using Harmony;
using UnityEngine;

namespace HCSLoader
{
	public static class GirlsWindowPaging
	{
		public static int GirlWindowOffset { get; set; }

		public const int GirlSlotCount = 18;

		public static void Update()
		{
			var activeWindow = Game.Manager.Windows.GetActiveWindow();

			if (activeWindow?.windowDefinition.id == 2)
			{
				UiGirlsWindow girlsWindow = activeWindow.GetComponent<UiGirlsWindow>();

				if (Input.GetKeyDown(KeyCode.F6)
					&& GirlWindowOffset > 0)
				{
					GirlWindowOffset -= GirlSlotCount;
					ReInitGirlWindow(girlsWindow);
				}
				else if (Input.GetKeyDown(KeyCode.F7)
						 && Game.Manager.Player.girls.Count > GirlWindowOffset + GirlSlotCount)
				{
					GirlWindowOffset += GirlSlotCount;
					ReInitGirlWindow(girlsWindow);
				}
			}
		}

		private static readonly FieldInfo GirlSlotsFieldInfo = AccessTools.Field(typeof(UiGirlsWindow), "_girlSlots");
		public static void ReInitGirlWindow(UiGirlsWindow window)
		{
			List<UiGirlsWindowSlot> girlSlots = (List<UiGirlsWindowSlot>)GirlSlotsFieldInfo.GetValue(window);

			if (GirlWindowOffset >= Game.Manager.Player.girls.Count || GirlWindowOffset < 0)
			{
				GirlWindowOffset = 0;
			}

			for (int i = 0; i < GirlSlotCount; i++)
			{
				var slot = girlSlots[i];

				slot.Init(i + GirlWindowOffset);
				slot.Refresh();
			}
		}

		
		[HarmonyPostfix, HarmonyPatch(typeof(UiGirlsWindow), "Start")]
		private static void ReInitGirlWindowHook(UiGirlsWindow __instance)
		{
			ReInitGirlWindow(__instance);
		}

		[HarmonyPostfix, HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.RecruitGirl))]
		private static void RecruitGirlFixHook()
		{
			int id = Game.Manager.Player.girls.Count - 1;

			Game.Manager.Player.girls[id].slotIndex = id;
		}
	}
}