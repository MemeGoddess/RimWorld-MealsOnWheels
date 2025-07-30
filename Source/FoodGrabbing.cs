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
		//public static bool TryFindBestFoodSourceFor(Pawn getter, Pawn eater, bool desperate, out Thing foodSource, out ThingDef foodDef, bool canRefillDispenser = true, bool canUseInventory = true, bool canUsePackAnimalInventory = false, bool allowForbidden = false, bool allowCorpse = true, bool allowSociallyImproper = false, bool allowHarvest = false, bool forceScanWholeMap = false, bool ignoreReservations = false, bool calculateWantedStackCount = false, FoodPreferability minPrefOverride = FoodPreferability.Undefined)
		[HarmonyPriority(Priority.Low)]
		public static void Postfix(ref bool __result, Pawn getter, Pawn eater, ref Thing foodSource, ref ThingDef foodDef, bool canUseInventory, bool canUsePackAnimalInventory)
		{
			if (eater.IsFreeColonist && __result == false && canUseInventory && canUsePackAnimalInventory &&
				getter.RaceProps.ToolUser && getter.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
			{
				Log.Message($"There be no food for " + eater);
				var pawns = eater.Map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer).FindAll(
					p => p != getter &&
					!p.Position.IsForbidden(getter) && 
					getter.CanReach(p, PathEndMode.OnCell, Danger.Some)
				);
				foreach (var p in pawns)
				{
					Log.Message($"Food soon rotten on " + p + "?");
					var thing = FoodUtility.BestFoodInInventory(p, eater, FoodPreferability.MealAwful);
					if (thing != null && thing.TryGetComp<CompRottable>() is CompRottable compRottable &&
						compRottable != null && compRottable.Stage == RotStage.Fresh && compRottable.TicksUntilRotAtCurrentTemp < GenDate.TicksPerDay / 2)
					{
						Log.Message($"Food is " + thing);
						foodSource = thing;
						foodDef = FoodUtility.GetFinalIngestibleDef(foodSource, false);
						__result = true;
						return;
					}
				}
				foreach (var p in pawns)
				{
					Log.Message($"Food on " + p + "?");
					var thing = FoodUtility.BestFoodInInventory(p, eater, FoodPreferability.DesperateOnly, FoodPreferability.MealLavish, 0f, !eater.IsTeetotaler());
					if (thing != null)
					{
						Log.Message($"Food is " + thing);
						foodSource = thing;
						foodDef = FoodUtility.GetFinalIngestibleDef(foodSource, false);
						__result = true;
						return;
					}
				}

				var shuttles = eater.Map.listerThings.AllThings.OfType<Building_PassengerShuttle>().ToList();
				shuttles.RemoveAll(x => x.Faction != Faction.OfPlayer);
				foreach (var shuttle in shuttles)
				{
					var innerContainer = shuttle.TryGetComp<CompTransporter>()?.innerContainer;
					for (int index = 0; index < innerContainer.Count; ++index)
					{
						Thing food = innerContainer[index];
						if (food.def.IsNutritionGivingIngestible && food.IngestibleNow && eater.WillEat(food, eater, allowVenerated: false) && food.def.ingestible.preferability >= FoodPreferability.DesperateOnly && food.def.ingestible.preferability <= FoodPreferability.MealLavish && (!eater.IsTeetotaler() || !food.def.IsDrug) && (double)FoodUtility.NutritionForEater(eater, food) * (double)food.stackCount >= (double)0f)
						{
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
	}
}
