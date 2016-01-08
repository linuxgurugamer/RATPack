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
	public class AudioSequence: IConfigNode
	{
		public AudioSequence next;
		public string clip;
		public AudioClip sound;

		public AudioSequence()
		{
			next = null;
			clip = "";
			sound = null;
		}

		public void Load(ConfigNode node)
		{
			if (node.HasNode ("next")) {
				next = new AudioSequence ();
				next.Load(node.GetNode ("next"));
			}
			if (node.HasValue ("clip")) {
				clip = node.GetValue ("clip");
				Debug.Log ("AudioSequence"+clip);
				sound = GameDatabase.Instance.GetAudioClip (clip);
			}
		}
		public void Save(ConfigNode node)
		{
			node.AddValue ("clip", clip);
			node.AddValue ("next", next);
		}
	}
}

