using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace RimWorld
{
	public static class TradeUtility
	{
		public const float MinimumBuyPrice = 0.5f;

		public const float MinimumSellPrice = 0.01f;

		public const float PriceFactorBuy_Global = 1.4f;

		public const float PriceFactorSell_Global = 0.6f;

		public static bool EverPlayerSellable(ThingDef def)
		{
			if (!def.tradeability.PlayerCanSell())
			{
				return false;
			}
			if (def.GetStatValueAbstract(StatDefOf.MarketValue) <= 0f)
			{
				return false;
			}
			if (def.category != ThingCategory.Item && def.category != ThingCategory.Pawn && def.category != ThingCategory.Building)
			{
				return false;
			}
			if (def.category == ThingCategory.Building && !def.Minifiable)
			{
				return false;
			}
			return true;
		}

		public static bool PlayerSellableNow(Thing t)
		{
			t = t.GetInnerIfMinified();
			if (!EverPlayerSellable(t.def))
			{
				return false;
			}
			if (t.IsNotFresh())
			{
				return false;
			}
			Apparel apparel = t as Apparel;
			if (apparel != null && apparel.WornByCorpse)
			{
				return false;
			}
			return true;
		}

		public static void SpawnDropPod(IntVec3 dropSpot, Map map, Thing t)
		{
			ActiveDropPodInfo activeDropPodInfo = new ActiveDropPodInfo();
			activeDropPodInfo.SingleContainedThing = t;
			activeDropPodInfo.leaveSlag = false;
			DropPodUtility.MakeDropPodAt(dropSpot, map, activeDropPodInfo);
		}

		public static IEnumerable<Thing> AllLaunchableThingsForTrade(Map map)
		{
			HashSet<Thing> yieldedThings = new HashSet<Thing>();
			foreach (Building_OrbitalTradeBeacon item in Building_OrbitalTradeBeacon.AllPowered(map))
			{
				foreach (IntVec3 tradeableCell in item.TradeableCells)
				{
					List<Thing> thingList = tradeableCell.GetThingList(map);
					for (int i = 0; i < thingList.Count; i++)
					{
						Thing t = thingList[i];
						if (t.def.category == ThingCategory.Item && PlayerSellableNow(t) && !yieldedThings.Contains(t))
						{
							yieldedThings.Add(t);
							yield return t;
							/*Error: Unable to find new state assignment for yield return*/;
						}
					}
				}
			}
			yield break;
			IL_01db:
			/*Error near IL_01dc: Unexpected return in MoveNext()*/;
		}

		public static IEnumerable<Pawn> AllSellableColonyPawns(Map map)
		{
			foreach (Pawn item in map.mapPawns.PrisonersOfColonySpawned)
			{
				if (item.guest.PrisonerIsSecure)
				{
					yield return item;
					/*Error: Unable to find new state assignment for yield return*/;
				}
			}
			foreach (Pawn item2 in map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer))
			{
				if (item2.RaceProps.Animal && item2.HostFaction == null && !item2.InMentalState && !item2.Downed && map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(item2.def))
				{
					yield return item2;
					/*Error: Unable to find new state assignment for yield return*/;
				}
			}
			yield break;
			IL_01c9:
			/*Error near IL_01ca: Unexpected return in MoveNext()*/;
		}

		public static Thing ThingFromStockToMergeWith(ITrader trader, Thing thing)
		{
			if (thing is Pawn)
			{
				return null;
			}
			foreach (Thing good in trader.Goods)
			{
				if (TransferableUtility.TransferAsOne(good, thing, TransferAsOneMode.Normal) && good.CanStackWith(thing) && good.def.stackLimit != 1)
				{
					return good;
				}
			}
			return null;
		}

		public static void LaunchThingsOfType(ThingDef resDef, int debt, Map map, TradeShip trader)
		{
			while (true)
			{
				if (debt <= 0)
				{
					return;
				}
				Thing thing = null;
				foreach (Building_OrbitalTradeBeacon item in Building_OrbitalTradeBeacon.AllPowered(map))
				{
					foreach (IntVec3 tradeableCell in item.TradeableCells)
					{
						foreach (Thing item2 in map.thingGrid.ThingsAt(tradeableCell))
						{
							if (item2.def != resDef)
							{
								continue;
							}
							thing = item2;
							goto IL_00cc;
						}
					}
				}
				goto IL_00cc;
				IL_00cc:
				if (thing == null)
				{
					break;
				}
				int num = Math.Min(debt, thing.stackCount);
				if (trader != null)
				{
					trader.GiveSoldThingToTrader(thing, num, TradeSession.playerNegotiator);
				}
				else
				{
					thing.SplitOff(num).Destroy();
				}
				debt -= num;
			}
			Log.Error("Could not find any " + resDef + " to transfer to trader.");
		}

		public static void LaunchSilver(Map map, int fee)
		{
			LaunchThingsOfType(ThingDefOf.Silver, fee, map, null);
		}

		public static Map PlayerHomeMapWithMostLaunchableSilver()
		{
			return (from x in Find.Maps
			where x.IsPlayerHome
			select x).MaxBy((Map x) => (from t in AllLaunchableThingsForTrade(x)
			where t.def == ThingDefOf.Silver
			select t).Sum((Thing t) => t.stackCount));
		}

		public static bool ColonyHasEnoughSilver(Map map, int fee)
		{
			return (from t in AllLaunchableThingsForTrade(map)
			where t.def == ThingDefOf.Silver
			select t).Sum((Thing t) => t.stackCount) >= fee;
		}

		public static void CheckInteractWithTradersTeachOpportunity(Pawn pawn)
		{
			if (!pawn.Dead)
			{
				Lord lord = pawn.GetLord();
				if (lord != null && lord.CurLordToil is LordToil_DefendTraderCaravan)
				{
					LessonAutoActivator.TeachOpportunity(ConceptDefOf.InteractingWithTraders, pawn, OpportunityType.Important);
				}
			}
		}

		public static float GetPricePlayerSell(Thing thing, float priceFactorSell_TraderPriceType, float priceGain_PlayerNegotiator, float priceGain_FactionBase)
		{
			float statValue = thing.GetStatValue(StatDefOf.SellPriceFactor);
			float num = thing.MarketValue * 0.6f * priceFactorSell_TraderPriceType * statValue * (1f - Find.Storyteller.difficulty.tradePriceFactorLoss);
			num *= 1f + priceGain_PlayerNegotiator + priceGain_FactionBase;
			num = Mathf.Max(num, 0.01f);
			if (num > 99.5f)
			{
				num = Mathf.Round(num);
			}
			return num;
		}

		public static float GetPricePlayerBuy(Thing thing, float priceFactorBuy_TraderPriceType, float priceGain_PlayerNegotiator, float priceGain_FactionBase)
		{
			float num = thing.MarketValue * 1.4f * priceFactorBuy_TraderPriceType * (1f + Find.Storyteller.difficulty.tradePriceFactorLoss);
			num *= 1f - priceGain_PlayerNegotiator - priceGain_FactionBase;
			num = Mathf.Max(num, 0.5f);
			if (num > 99.5f)
			{
				num = Mathf.Round(num);
			}
			return num;
		}
	}
}
