using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Harmony;
using UnityEngine;

namespace HCSLoader
{
	public static class WardrobeWindowPaging
	{
		public static int WardrobeGirlOffset { get; set; }

		public static void Update()
		{
			var activeWindow = Game.Manager.Windows.GetActiveWindow();

			if (activeWindow?.windowDefinition.id == 13)
			{
				UiWardrobeWindow wardrobeWindow = activeWindow.GetComponent<UiWardrobeWindow>();

				if (Input.GetKeyDown(KeyCode.F6)
					&& WardrobeGirlOffset > 0)
				{
					WardrobeGirlOffset -= GirlsWindowPaging.GirlSlotCount;
					ReInitWardrobeWindow(wardrobeWindow);
				}
				else if (Input.GetKeyDown(KeyCode.F7)
						 && Game.Persistence.saveData.wardrobeGirls.Count > WardrobeGirlOffset + GirlsWindowPaging.GirlSlotCount)
				{
					WardrobeGirlOffset += GirlsWindowPaging.GirlSlotCount;
					ReInitWardrobeWindow(wardrobeWindow);
				}
			}
		}

		private static readonly FieldInfo GirlDefinitionFieldInfo = AccessTools.Field(typeof(UiWardrobeWindowSlot), "_girlDefinition");
		private static readonly FieldInfo WardrobeGirlSaveDataFieldInfo = AccessTools.Field(typeof(UiWardrobeWindowSlot), "_wardrobeGirlSaveData");
		private static readonly MethodInfo WardrobeWindowRefreshMethodInfo = AccessTools.Method(typeof(UiWardrobeWindow), "Refresh");
		public static void ReInitWardrobeWindow(UiWardrobeWindow window)
		{
			for (int i = 0; i < GirlsWindowPaging.GirlSlotCount; i++)
			{
				var slot = window.girlSlots[i];
				int slotOffset = slot.slotIndex + WardrobeGirlOffset;

				if (slotOffset >= Game.Persistence.saveData.wardrobeGirls.Count)
				{
					GirlDefinitionFieldInfo.SetValue(slot, null);
					WardrobeGirlSaveDataFieldInfo.SetValue(slot, null);
				}
				else
				{
					var girl = Game.Persistence.saveData.wardrobeGirls[slotOffset];

					GirlDefinitionFieldInfo.SetValue(slot, Game.Data.Girls.Get(girl.girlId));
					WardrobeGirlSaveDataFieldInfo.SetValue(slot, girl);
				}

				slot.Refresh(false);
			}


			//fix for ghost slot when paging
			if (window.girlSlots[UiWardrobeWindow.storedSlotIndex].girlDefinition != null)
				window.girlSlots[UiWardrobeWindow.storedSlotIndex].buttonBehavior.Enable(false);
			
			UiWardrobeWindow.storedSlotIndex = 0;
			window.girlSlots[UiWardrobeWindow.storedSlotIndex].buttonBehavior.Disable(false);
			
			WardrobeWindowRefreshMethodInfo.Invoke(window, new object[] { false, true });


			//fix for ghost tooltip when paging
			Game.Events.Trigger(new InfoTooltipExitEvent());
		}
		

		
		[HarmonyPostfix, HarmonyPatch(typeof(UiWardrobeWindow), "Start")]
		private static void ReInitGirlWindowHook(UiWardrobeWindow __instance)
		{
			ReInitWardrobeWindow(__instance);
		}

		[HarmonyTranspiler, HarmonyPatch(typeof(UiWardrobeWindowSlot), nameof(UiWardrobeWindowSlot.Refresh))]
		private static IEnumerable<CodeInstruction> RecruitGirlFixTranspiler(IEnumerable<CodeInstruction> instructions)
		{
			int i = 0;

			foreach (var instruction in instructions)
			{
				if (i++ == 8)
				{
					yield return instruction;
					yield return new CodeInstruction(OpCodes.Br, instruction.operand);

					i++;
				}
				else
				{
					yield return instruction;
				}
			}
		}
	}
}