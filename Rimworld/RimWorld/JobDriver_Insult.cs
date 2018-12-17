using System;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace RimWorld
{
	public class JobDriver_Insult : JobDriver
	{
		private const TargetIndex TargetInd = TargetIndex.A;

		private Pawn Target => (Pawn)(Thing)pawn.CurJob.GetTarget(TargetIndex.A);

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			return true;
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOnDespawnedOrNull(TargetIndex.A);
			yield return Toils_Interpersonal.GotoInteractablePosition(TargetIndex.A);
			/*Error: Unable to find new state assignment for yield return*/;
		}

		private Toil InteractToil()
		{
			return Toils_General.Do(delegate
			{
				if (pawn.interactions.TryInteractWith(Target, InteractionDefOf.Insult))
				{
					MentalState_InsultingSpree mentalState_InsultingSpree = pawn.MentalState as MentalState_InsultingSpree;
					if (mentalState_InsultingSpree != null)
					{
						mentalState_InsultingSpree.lastInsultTicks = Find.TickManager.TicksGame;
						if (mentalState_InsultingSpree.target == Target)
						{
							mentalState_InsultingSpree.insultedTargetAtLeastOnce = true;
						}
					}
				}
			});
		}

		private Toil InsultingSpreeDelayToil()
		{
			Action action = delegate
			{
				MentalState_InsultingSpree mentalState_InsultingSpree = pawn.MentalState as MentalState_InsultingSpree;
				if (mentalState_InsultingSpree == null || Find.TickManager.TicksGame - mentalState_InsultingSpree.lastInsultTicks >= 1200)
				{
					pawn.jobs.curDriver.ReadyForNextToil();
				}
			};
			Toil toil = new Toil();
			toil.initAction = action;
			toil.tickAction = action;
			toil.socialMode = RandomSocialMode.Off;
			toil.defaultCompleteMode = ToilCompleteMode.Never;
			return toil;
		}
	}
}
