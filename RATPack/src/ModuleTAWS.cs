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
	internal class FlightHistSample
	{
		public float RadarHeight { get; set; }
		public float RadarContact { get; set; }
		public double UT { get; set; }

		public FlightHistSample(Vessel vessel)
		{
			UT = Planetarium.GetUniversalTime ();

			RadarHeight = GetRadarAltitude (vessel);

			RadarContact = float.NaN;
		}

		static public float GetRadarAltitude(Vessel vessel)
		{
			float radarHeight = (float)(vessel.terrainAltitude > 0 ? vessel.heightFromTerrain : vessel.altitude);
			if (radarHeight < 0.0f) {
				radarHeight = (float)(vessel.altitude - vessel.terrainAltitude);
				if (radarHeight < 0.0f) {
					radarHeight = (float)vessel.altitude;
				}
				if (radarHeight < 0.0f && vessel.situation == Vessel.Situations.ORBITING) {
					radarHeight = (float)vessel.orbit.altitude;
				}
				if (radarHeight < 0.0f) {
					radarHeight = float.NaN;
				}
			}
			return radarHeight;
		}
	}



	internal class TAWSPreset
	{
		public float LandingSpeed { get; set; }
		public float MaxAltitude { get; set; }
		public float MaxSpeed { get; set; }
		public float LandingTolerance { get; set; }
		public float LateralInclusion { get; set; }
		public string Title { get; set; }


		public TAWSPreset (float landingSpeed, float maxAltitude, float maxSpeed, float landingTolerance, float lateralInc, string title)
		{
			LandingSpeed = landingSpeed;
			MaxAltitude = maxAltitude;
			MaxSpeed = maxSpeed;
			LandingTolerance = landingTolerance;
			LateralInclusion = lateralInc;
			Title = title;
		}

		public override bool Equals (object obj)
		{
			if (obj is TAWSPreset) {
				TAWSPreset rhs = (TAWSPreset)obj;
				if (LandingSpeed != rhs.LandingSpeed)
					return false;
				if (MaxAltitude != rhs.MaxAltitude)
					return false;
				if (MaxSpeed != rhs.MaxSpeed)
					return false;
				if (LandingTolerance != rhs.LandingTolerance)
					return false;
				if (LateralInclusion != rhs.LateralInclusion)
					return false;
				return true;
			}
			return base.Equals (obj);
		}
		public override int GetHashCode ()
		{
			return (int)LandingSpeed ^ (int)MaxAltitude ^ (int)MaxSpeed;
		}
	}

	public class ModuleTAWS: PartModule
	{
		static ModuleTAWS _warningActiveModule = null;

		const double SAMPLE_INTERVAL = 0.1d;

		[KSPField]
		public float sampleWindow = 5.0f;

		[KSPField]
		public float fltrChargeRate = 1.0f;

		[KSPField]
		public bool forwardLookingRadar = true;

		[KSPField]
		public string scanTransform = "";

		[KSPField(guiActive=true,guiName="Descent Rate",guiFormat="F3")]
		public float measuredDescentRate = 0.0f;

		[KSPField(guiActive=true,guiName="Radar Alt",guiFormat="F3")]
		public float radarAltitude;

		[KSPField(guiActive=true,guiName="Radar Dist",guiFormat="F3")]
		public float radarDistance;

		[KSPField(isPersistant=true,guiActive=true,guiActiveEditor=true,guiName="Audio Alert:"),
			UI_Toggle(disabledText="Silent",enabledText="Active")]
		public bool audioOutput = true;

		[KSPField(isPersistant=true,guiActive=true,guiActiveEditor=true,guiName="FLT Radar:"),
			UI_Toggle(disabledText="Disabled",enabledText="Enabled")]
		public bool fltrRadar = true;

		[KSPField(isPersistant=true,guiActiveEditor=true,guiActive=true,guiName="Landing Speed"),
			UI_FloatRange(minValue=0.0f,maxValue=20.0f)]
		public float warnApproachVelMin = 10.0f;

		[KSPField(isPersistant=true,guiActiveEditor=true,guiActive=true,guiName="Max Altitude"),
			UI_FloatRange(minValue=100.0f,maxValue=2000.0f)]
		public float warnAltitudeMax = 1000.0f;

		[KSPField(isPersistant=true,guiActiveEditor=true,guiActive=true,guiName="Max Speed"),
			UI_FloatRange(minValue=100.0f,maxValue=1000.0f)]
		public float warnApproachVelMax = 400.0f;

		[KSPField(isPersistant=true,guiActiveEditor=true,guiActive=true,guiName="Lateral Incl."),
			UI_FloatRange(minValue=0.5f,maxValue=20.0f,stepIncrement=0.5f)]
		public float lateralInclusion = 2.0f;

		[KSPField(isPersistant=true,guiActiveEditor=true,guiActive=true,guiName="Landing Tolerance"),
			UI_FloatRange(minValue=1f,maxValue=5.0f,stepIncrement=0.1f)]
		public float landingTolerance = 1.5f;

		[KSPField(guiActive=true,guiActiveEditor=true,guiName="Preset")]
		public string presetString = "Default";

		[KSPField]
		public  AudioSequence 			terrainAudio = new AudioSequence();

		private AudioSequence 			_playing = null;
		private AudioSource 			_audioSource = null;
		private double 					_prevTime = 0.0d;
		private List<FlightHistSample> 	_flightHist = new List<FlightHistSample> ();
		private Texture2D				_radar = new Texture2D(400,400);
		private Rect 					_windowPos = new Rect();
		private Transform 				_transform = null;
		private bool 					_radarVisible = false;
		private int 					_winID = 1;
		private double 					_horizontalDir = 1.0d;
		private bool					_warningActive = false;
		private double 					_warningTime = 0d;
		private TAWSPreset				_current = new TAWSPreset(0f,0f,0f,0f,0f,"Current");
		private List<TAWSPreset> _presets = new List<TAWSPreset> {
			new TAWSPreset(10f,1000f,200f,1.2f,4.0f,"Safety First"), 
			new TAWSPreset(10f,1000f,400f,1.5f,2.0f,"Default"),
			new TAWSPreset(10f,800f,600f,2.0f, 2.0f,"Fearless"),
			new TAWSPreset(10f,500f,500f,3.0f, 1.0f, "Widowmaker"),
		};
		/// <summary>
		/// Called when the flight starts, or when the part is created in the editor. OnStart will be called
		///  before OnUpdate or OnFixedUpdate are ever called.
		/// </summary>
		/// <param name="state">Some information about what situation the vessel is starting in.</param>
		public override void OnStart(StartState state)
		{
			_audioSource = gameObject.AddComponent<AudioSource> ();
			if (terrainAudio != null && terrainAudio.sound == null) {
				terrainAudio = part.partInfo.partPrefab.Modules.GetModules<ModuleTAWS> ()[0].terrainAudio;
			}
			_audioSource.dopplerLevel = 0.0f;
			_audioSource.panLevel = 0.0f;
			_audioSource.enabled = true;
			_audioSource.Stop ();
			_winID = GUIUtility.GetControlID (FocusType.Passive);

			_prevTime = Planetarium.GetUniversalTime () + 2.0d;
			if (!forwardLookingRadar) {
				Fields ["fltrRadar"].guiActive = false;
				Fields ["fltrRadar"].guiActiveEditor = false;
				fltrRadar = false;
			}

			_transform = part.transform;
			if (scanTransform.Length > 0) {
				Transform tempTransform = part.FindModelTransform (scanTransform);
				if (tempTransform != null) {
					_transform = tempTransform;
				}
			}
			_warningActiveModule = null;

		}
		public override void OnInactive ()
		{
			base.OnInactive ();
			_warningActiveModule = null;
		}

		public void Update()
		{
			_current.LandingSpeed = warnApproachVelMin;
			_current.MaxAltitude = warnAltitudeMax;
			_current.MaxSpeed = warnApproachVelMax;
			_current.LandingTolerance = landingTolerance;
			_current.LateralInclusion = lateralInclusion;

			TAWSPreset taws = _presets.Find (preset => preset.Equals (_current));
			if (taws == null) {
				presetString = "Custom";
			} else {
				presetString = taws.Title;
			}
			CheckTerrainWarning ();
		}

		/// <summary>
		/// Called on physics update. Determine if we should issue a warning.
		/// </summary>
		public void FixedUpdate()
		{
			if (vessel == null) {
				return;
			}

			double ut = Planetarium.GetUniversalTime ();
			radarAltitude = FlightHistSample.GetRadarAltitude (vessel);
			if (ut - _prevTime >= SAMPLE_INTERVAL) {
				double deltaTime = ut - _prevTime;
				_prevTime = ut;
				FlightHistSample sample = new FlightHistSample (vessel);
				if (fltrRadar && radarAltitude <  warnAltitudeMax * 1.2f && vessel.situation != Vessel.Situations.LANDED) {
					part.RequestResource ("ElectricCharge", deltaTime * fltrChargeRate);
					sample.RadarContact = ForwardRadar (Math.PI / 4);
				}
				radarDistance = sample.RadarContact;
				_flightHist.Add (sample);

				while (_flightHist.Count > 2 && (sample.UT - _flightHist [0].UT) > sampleWindow) {
					_flightHist.RemoveAt (0);
				}
			}
			if (_flightHist.Count < 2)
				return;

			FlightHistSample oldest = _flightHist [0];
			FlightHistSample newest = _flightHist [_flightHist.Count - 1];

			double descentRate = (oldest.RadarHeight - newest.RadarHeight) / (newest.UT - oldest.UT);
			if (descentRate < -vessel.verticalSpeed)
				descentRate = -vessel.verticalSpeed;
			measuredDescentRate = (float)descentRate;

			bool overHeight = newest.RadarHeight > warnAltitudeMax && newest.RadarContact > warnAltitudeMax;

			double tolerance = 1.0d;
			bool gearDown = IsGearDown ();
			if (gearDown)
				tolerance = (double)landingTolerance;

			double descentRateThreshold = (warnApproachVelMax - warnApproachVelMin) * (newest.RadarHeight / warnAltitudeMax) * tolerance +
				warnApproachVelMin;
			double approachRateThreshold = 0.0d;
			if (!float.IsNaN (newest.RadarContact)) {
				approachRateThreshold = (warnApproachVelMax - warnApproachVelMin) * (newest.RadarContact / warnAltitudeMax) * tolerance +
					warnApproachVelMin;
			}

			if (!overHeight && descentRate > descentRateThreshold ) {
				if (!_warningActive) {
					_warningActive = true;
					_warningTime = ut;
					Debug.Log("TAWS - Descent Rate:" + descentRate + " Gear Down:"+gearDown);
				}
				TerrainWarning ();
			} else if (!overHeight &&  !float.IsNaN(newest.RadarContact) && newest.RadarContact < warnAltitudeMax &&
				Vector3.Dot(vessel.srf_velocity,_transform.forward) > approachRateThreshold) {
				if (!_warningActive) {
					_warningActive = true;
					_warningTime = ut;
					Debug.Log("TAWS - Approach Rate:" + Vector3.Dot(vessel.srf_velocity,_transform.forward)+ " Gear Down:"+gearDown);
				}
				TerrainWarning ();
			} else {
				_playing = null;
				_audioSource.Stop ();
				_warningActive = false;
				if (_warningActiveModule == this)
					_warningActiveModule = null;
			}
		}

		private bool IsGearDown()
		{
			List<ModuleLandingGear> gears = vessel.FindPartModulesImplementing<ModuleLandingGear> ();
			foreach (ModuleLandingGear gear in gears) {
				if (gear.gearState == ModuleLandingGear.GearStates.DEPLOYED)
					return true;
			}
			List<ModuleAdvancedLandingGear> advGears = vessel.FindPartModulesImplementing<ModuleAdvancedLandingGear> ();
			foreach (ModuleAdvancedLandingGear gear in advGears) {
				if (gear.gearState == ModuleAdvancedLandingGear.GearStates.DEPLOYED)
					return true;
			}
			List<ModuleLandingLeg> legs = vessel.FindPartModulesImplementing<ModuleLandingLeg> ();
			foreach (ModuleLandingLeg leg in legs) {
				if (leg.legState == ModuleLandingLeg.LegStates.DEPLOYED)
					return true;
			}
			return false;
		}

		public void OnDraw()
		{
			_windowPos = GUILayout.Window (_winID, _windowPos, OnWindow, "TAWS");
		}

		public void OnWindow(int windowID)
		{
			GUILayout.BeginVertical (GUILayout.Width(500.0f),GUILayout.Height(410.0f));
			GUILayout.Box (_radar);
			GUILayout.BeginHorizontal ();
			if (GUILayout.Button ("Flip Horizontal")) {
				_horizontalDir *= -1.0d;
			}
			if (GUILayout.Button ("Close")) {
				ToggleViewRadar ();
			}
			GUILayout.EndHorizontal ();
			GUILayout.EndVertical ();
			GUI.DragWindow ();
		}

		/// <summary>
		/// Sets the Terrain warning.
		/// </summary>
		private void TerrainWarning()
		{
			if (_warningActiveModule != null && _warningActiveModule != this) {
				return;
			} else {
				_warningActiveModule = this;
			}
		}
		/// <summary>
		/// Plays the Terrain warning if conditions are met.
		/// </summary>
		private void CheckTerrainWarning()
		{
			if (_warningActiveModule == this) {
				if (audioOutput && !_audioSource.isPlaying && terrainAudio != null) {
					if (_playing == null) {
						_playing = terrainAudio;
					}
					if (_playing.sound != null) {
						_audioSource.clip = _playing.sound;
						_audioSource.Play ();
					}
					if (_playing != null) {
						_playing = _playing.next;
					}
				}
				ScreenMessages.PostScreenMessage ("TAWS: Terrain! Pull Up!");
			}
		}

		/// <summary>
		/// Scans forward and returns the nearest contact within the warning lateral exclusion range.
		/// </summary>
		/// <returns>The radar range or NaN.</returns>
		/// <param name="sweepAngle">Sweep angle.</param>
		private float ForwardRadar(double sweepAngle)
		{
			float yscale = (float)_radar.height / warnAltitudeMax;
			float xscale = (float)_radar.width / (warnAltitudeMax * 2 * (float)Math.Sin(sweepAngle/2));
			int exclusionX = (int)(lateralInclusion * xscale) + _radar.width / 2;
			if (_radarVisible) {
				for (int y = 0; y < _radar.height; y++) {
					for (int x = 0; x < _radar.width; x++) {
						double scaledY = y / 2;
						double scaledX = (x - _radar.width / 2) * Math.Sin (sweepAngle / 2);

						double angle = Math.Atan2 (scaledX, scaledY);
						double distance = Math.Sqrt (Math.Pow (Math.Abs (scaledX), 2) + Math.Pow (scaledY, 2));

						if (Math.Abs (angle) <= sweepAngle / 2 && distance <= (double)_radar.width / 2) {
							_radar.SetPixel (x, y, Color.black);
						} else {
							_radar.SetPixel (x, y, Color.gray);
						}
					}
				}
			}

			float minDistance = float.NaN;
			for (double angle = -sweepAngle / 2; angle <= sweepAngle / 2; angle += sweepAngle/128) {
				float distance = RadarBeam (angle,0d);

				if (float.IsNaN (distance)) {
					continue;
				}
				double y = distance * Math.Cos (angle);
				double x = distance * Math.Sin (angle);
				Color point = Color.green;
				if (Math.Abs (x) < lateralInclusion) {
					if (distance < minDistance) {
						minDistance = distance;
					}

					if (float.IsNaN (minDistance)) {
						minDistance = distance;
					}
					point = Color.red;
				}

				if (_radarVisible) {
					int xpoint = (int)(_horizontalDir * x * xscale) + _radar.width / 2;
					int ypoint = (int)(y * yscale);

					_radar.SetPixel (xpoint + 1, ypoint, point);
					_radar.SetPixel (xpoint, ypoint + 1, point);
					_radar.SetPixel (xpoint + 1, ypoint + 1, point);
					_radar.SetPixel (xpoint, ypoint, point);
				}
			}
			_radar.Apply ();
			return minDistance;
		}

		/// <summary>
		/// Radar beam.
		/// </summary>
		/// <returns>Any contact.</returns>
		/// <param name="angle">Angle.</param>
		/// <param name="rotation">Rotation.</param>
		private float RadarBeam(double angle, double rotation)
		{
			float distance = float.NaN;

			Vector3 forward = _transform.forward;
			Vector3 baseup = _transform.up;
			Vector3 up = Quaternion.AngleAxis ((float)(rotation*180.0d/Math.PI), forward) * baseup;
			Vector3 direction = Quaternion.AngleAxis ((float)(angle*180.0d/Math.PI), up) * forward;
			Ray ray = new Ray (_transform.position, direction);
			RaycastHit hit = new RaycastHit ();
			if (Physics.Raycast(ray,out hit,warnAltitudeMax,(1<<15))) {
				distance = hit.distance;
			}
			return distance;
		}

		/// <summary>
		/// Gets the description for this part.
		/// </summary>
		/// <returns>The info.</returns>
		public override string GetInfo ()
		{

			return "Forward Looking Terrain Radar:"+(forwardLookingRadar ? "Yes" : "No") + "\n"+
				"Terrain Radar Electric Charge:"+fltrChargeRate+"\n";
		}
		[KSPEvent(guiActive=true,guiActiveEditor=true,guiName="Next Preset")]
		public void NextPreset()
		{
			int idx = _presets.FindIndex (preset => preset.Equals (_current));
			if (idx >= 0) { 
				idx++;
				if (idx >= _presets.Count)
					idx = 0;
			} else {
				idx = 0;
			}
			TAWSPreset taws = _presets[idx];
			warnApproachVelMin = taws.LandingSpeed;
			warnAltitudeMax = taws.MaxAltitude;
			warnApproachVelMax = taws.MaxSpeed;
			lateralInclusion = taws.LateralInclusion;
			landingTolerance = taws.LandingTolerance;
			presetString = taws.Title;
		}
		[KSPEvent(guiActive=true,guiName="Toggle Radar View",unfocusedRange=5f,guiActiveUnfocused=true)]
		public void ToggleViewRadar()
		{
			_radarVisible = !_radarVisible;
			if (_radarVisible)
				RenderingManager.AddToPostDrawQueue (0, OnDraw);
			else
				RenderingManager.RemoveFromPostDrawQueue (0, OnDraw);
		}

		[KSPAction("Toggle FLT Radar")]
		public void ToggleFLTRadar(KSPActionParam param)
		{
			fltrRadar = !fltrRadar;
		}
	}
}

