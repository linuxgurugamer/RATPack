/*
 * Copyright 2015 SatNet
 * 
 * This file is subject to the included LICENSE.md file. 
 */

using KSP.Localization;
using System;
using System.Collections.Generic;
using UnityEngine;

using KSP.UI.Screens;

using Icon = RUI.Icons.Selectable.Icon;

namespace RATPack
{
	[KSPAddon(KSPAddon.Startup.Instantly,true)]
	public class PartFilter: MonoBehaviour
	{
		public void Start()
		{
			DontDestroyOnLoad (this);
			GameEvents.onGUIEditorToolbarReady.Add (Categorize);
		}

		public void OnDestroy()
		{
			GameEvents.onGUIEditorToolbarReady.Remove (Categorize);
		}

		/// <summary>
		/// Categorize parts.
		/// </summary>
		public void Categorize()
		{
			Texture imgBlack = (Texture)GameDatabase.Instance.GetTexture (Localizer.Format("#LOC_RAT_67"), false);
			Texture imgWhite = (Texture)GameDatabase.Instance.GetTexture (Localizer.Format("#LOC_RAT_68"), false);
			Icon icon = new Icon (Localizer.Format("#LOC_RAT_69"), imgBlack, imgWhite);

			Icon rats = PartCategorizer.Instance.iconLoader.GetIcon (Localizer.Format("#LOC_RAT_70"));
			Icon thrustReverse = PartCategorizer.Instance.iconLoader.GetIcon (Localizer.Format("#LOC_RAT_71"));
			Icon taws = PartCategorizer.Instance.iconLoader.GetIcon (Localizer.Format("#LOC_RAT_72"));

			PartCategorizer.Category cat = PartCategorizer.AddCustomFilter (Localizer.Format("#LOC_RAT_73"), Localizer.Format("#LOC_RAT_73"), icon, Color.gray);
			cat.displayType = EditorPartList.State.PartsList;

			// All of the parts.
			PartCategorizer.AddCustomSubcategoryFilter (cat, Localizer.Format("#LOC_RAT_73"), Localizer.Format("#LOC_RAT_73"), icon, p => p.manufacturer.Contains (Localizer.Format("#LOC_RAT_74")));

			// Rats.
			PartCategorizer.AddCustomSubcategoryFilter (cat, Localizer.Format("#LOC_RAT_75"), Localizer.Format("#LOC_RAT_75"), rats, p => p.moduleInfos.Exists(m=>m.moduleName.Contains(Localizer.Format("#LOC_RAT_69"))));

			// Find TRs via title because module name doesn't seem to work.
			PartCategorizer.AddCustomSubcategoryFilter (cat, Localizer.Format("#LOC_RAT_76"), Localizer.Format("#LOC_RAT_76"), thrustReverse, p => p.title.Contains(Localizer.Format("#LOC_RAT_77")));

			// TAWS.
			PartCategorizer.AddCustomSubcategoryFilter (cat, Localizer.Format("#LOC_RAT_36"), Localizer.Format("#LOC_RAT_36"), taws, 
				p => p.moduleInfos.Exists(m=>(m.moduleName.Contains(Localizer.Format("#LOC_RAT_36"))))||p.title.Contains(Localizer.Format("#LOC_RAT_43")) );
		}
	}
}

