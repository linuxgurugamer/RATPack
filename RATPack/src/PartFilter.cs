/*
 * Copyright 2015 SatNet
 * 
 * This file is subject to the included LICENSE.md file. 
 */

using System;
using System.Collections.Generic;
using UnityEngine;
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
			Texture imgBlack = (Texture)GameDatabase.Instance.GetTexture ("RATPack/Textures/raticon", false);
			Texture imgWhite = (Texture)GameDatabase.Instance.GetTexture ("RATPack/Textures/raticon_white", false);
			Icon icon = new Icon ("RAT", imgBlack, imgWhite);

			Icon rats = PartCategorizer.Instance.iconLoader.GetIcon ("R&D_node_icon_advelectrics");
			Icon thrustReverse = PartCategorizer.Instance.iconLoader.GetIcon ("RDicon_aerospaceTech2");
			Icon taws = PartCategorizer.Instance.iconLoader.GetIcon ("R&D_node_icon_highaltitudeflight");

			PartCategorizer.Category cat = PartCategorizer.AddCustomFilter ("RATPack", icon, Color.gray);
			cat.displayType = EditorPartList.State.PartsList;

			// All of the parts.
			PartCategorizer.AddCustomSubcategoryFilter (cat, "RATPack", icon, p => p.manufacturer.Contains ("SatNet"));

			// Rats.
			PartCategorizer.AddCustomSubcategoryFilter (cat, "RATs", rats, p => p.moduleInfos.Exists(m=>m.moduleName.Contains("RAT")));

			// Find TRs via title because module name doesn't seem to work.
			PartCategorizer.AddCustomSubcategoryFilter (cat, "Thrust Reversers", thrustReverse, p => p.title.Contains("Thrust Reverse"));

			// TAWS.
			PartCategorizer.AddCustomSubcategoryFilter (cat, "TAWS", taws, 
				p => p.moduleInfos.Exists(m=>(m.moduleName.Contains("TAWS")))||p.title.Contains("Terrain Radar") );
		}
	}
}

