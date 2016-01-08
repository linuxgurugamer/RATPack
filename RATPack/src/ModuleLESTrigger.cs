/*
 * Copyright 2015 SatNet
 * 
 * This file is subject to the included LICENSE.md file. 
 */

using System;
using System.Collections.Generic;
using UnityEngine;

namespace RATPack
{
	public class FARVesselListener: VesselModule
	{
		public void AerodynamicFailureStatus()
		{
			Vessel vessel = GetComponent<Vessel> ();
			if (vessel != null) {
				List<ModuleLESTrigger> triggers = vessel.FindPartModulesImplementing<ModuleLESTrigger> ();
				foreach (ModuleLESTrigger trigger in triggers) {
					trigger.AerodynamicFailureStatus ();
				}
			}
		}
	}
	public class ModuleLESTrigger: PartModule
	{
		[KSPField(isPersistant=true,guiActive=true,guiActiveEditor=true,guiName="Auto Abort:"),
			UI_Toggle(disabledText="Inactive",enabledText="Active")]
		public bool autoAbort = true;
		/// <summary>
		/// Called when the flight starts, or when the part is created in the editor. OnStart will be called
		///  before OnUpdate or OnFixedUpdate are ever called.
		/// </summary>
		/// <param name="state">Some information about what situation the vessel is starting in.</param>
		public override void OnStart(StartState state)
		{
			if (vessel != null) {
				GameEvents.onPartDie.Add (OnPartDie);
			}
		}
		public override void OnInactive ()
		{
			GameEvents.onPartDie.Remove (OnPartDie);
		}
		public void AerodynamicFailureStatus()
		{
			if (autoAbort) {
				Debug.Log ("LEST: Aero Failure");
				vessel.ActionGroups.SetGroup (KSPActionGroup.Abort, true);
			}
		}
		private void OnPartDie(Part part)
		{
			if (autoAbort && part.vessel == vessel) {
				Debug.Log ("LEST: Part Failure - " + part.partInfo.title);
				vessel.ActionGroups.SetGroup (KSPActionGroup.Abort, true);
			}
		}
	}
}

