using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace Meals_On_Wheels
{
	[HarmonyPatch(typeof(FoodUtility), nameof(FoodUtility.TryFindBestFoodSourceFor))]
	class FoodGrabbing
	{
		[HarmonyPriority(Priority.Low)]
		public static void Postfix(ref bool __result, Pawn getter, Pawn eater, ref Thing foodSource, ref ThingDef foodDef, bool canUseInventory, bool canUsePackAnimalInventory)
		{
			if (__result || !eater.IsFreeColonist || !canUseInventory || !canUsePackAnimalInventory ||
			    !getter.RaceProps.ToolUser || !getter.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
			{
				Log.Message($"There be no food for " + eater);
				return;
			}

			var pawns = eater.Map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer).FindAll(
				p => p != getter &&
				     !p.Position.IsForbidden(getter) && 
				     getter.CanReach(p, PathEndMode.OnCell, Danger.Some) &&
						 (p.inventory?.innerContainer?.Any() ?? false)
			);

			foreach (var p in pawns)
			{
				Log.Message($"Food soon rotten on " + p + "?");
				var thing = FoodUtility.BestFoodInInventory(p, eater, FoodPreferability.MealAwful);
				if (thing?.TryGetComp<CompRottable>() is not
				    { Stage: RotStage.Fresh, TicksUntilRotAtCurrentTemp: < GenDate.TicksPerDay / 2 }) continue;

				Log.Message($"Food is " + thing);
				foodSource = thing;
				foodDef = FoodUtility.GetFinalIngestibleDef(foodSource, false);
				__result = true;

				return;
			}

			foreach (var p in pawns)
			{
				Log.Message($"Food on " + p + "?");
				var thing = FoodUtility.BestFoodInInventory(p, eater, FoodPreferability.DesperateOnly, FoodPreferability.MealLavish, 0f, !eater.IsTeetotaler());
				if (thing == null) continue;

				Log.Message($"Food is " + thing);
				foodSource = thing;
				foodDef = FoodUtility.GetFinalIngestibleDef(foodSource, false);
				__result = true;
				return;
			}

			if (!ModsConfig.OdysseyActive)
				return;

			var shuttles = eater.Map.listerThings.ThingsOfDef(ThingDefOf.PassengerShuttle).Where(x => x.Faction == Faction.OfPlayer);
			foreach (var shuttle in shuttles)
			{
				var innerContainer = shuttle.TryGetComp<CompTransporter>()?.innerContainer;
				for (var index = 0; index < innerContainer.Count; ++index)
				{
					var food = innerContainer[index];
					if (!food.def.IsNutritionGivingIngestible || !food.IngestibleNow ||
					    !eater.WillEat(food, eater, allowVenerated: false) ||
					    food.def.ingestible.preferability < FoodPreferability.DesperateOnly ||
					    food.def.ingestible.preferability > FoodPreferability.MealLavish ||
					    (eater.IsTeetotaler() && food.def.IsDrug) ||
					    !(FoodUtility.NutritionForEater(eater, food) * (double)food.stackCount >= (double)0f)) continue;


					innerContainer.TryDrop(food, ThingPlaceMode.Near, 1, out _);

					Log.Message($"Food is " + food);
					foodSource = food;
					foodDef = FoodUtility.GetFinalIngestibleDef(foodSource, false);
					__result = true;
					return;
				}
			}
		}
	}
}
