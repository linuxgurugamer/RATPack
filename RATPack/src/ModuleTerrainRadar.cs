using System;
using System.Collections.Generic;
using UnityEngine;

namespace RATPack
{
	public class ModuleTerrainRadar: PartModule
	{
		static ModuleTerrainRadar _activeRadar = null;
		[KSPField]
		public string scanTransform = "";
		[KSPField(isPersistant=true)]
		public int 				scaleMode = 3;
		[KSPField(isPersistant=true)]
		public int 				referenceMode = 2;
		[KSPField(isPersistant=true)]
		public float 			scanRadius = 100f;
		[KSPField(isPersistant=true)]
		public int				scanMode = 0;
		[KSPField(isPersistant=true,guiActive=true,guiActiveEditor=true,guiName="Ping:"),
			UI_Toggle(disabledText="Silent",enabledText="Active")]
		public bool audioOutput = false;
		[KSPField]
		public FloatCurve altitudeCurve = new FloatCurve(new Keyframe[]
			{
				new Keyframe(0.0f,0.0f,0.0f,0.0f),
				new Keyframe(50.0f,0.1f,0.0f,0.0f),
				new Keyframe(100.0f,0.5f,0.0f,0.0f),
				new Keyframe(1000.0f,4f,0.0f,0.0f)
			});
		[KSPField]
		public AudioSequence blipSound = new AudioSequence ();

		private Transform 		_transform = null;
		private bool 			_radarVisible = false;
		private int 			_winID = 1;
		private Rect 			_windowPos = new Rect();
		private double			_prevTime = 0d;
		private float 			_scale = 10.0f;
		private float			_max = float.NaN;
		private float			_min = float.NaN;
		private Texture2D 		_scaleGraph = new Texture2D(20,300);
		private Texture2D 		_terrain = new Texture2D(300,300);
		private Texture2D 		_terrainLateral = new Texture2D (300, 100);
		private List<Color> 	_colorScheme = new List<Color> {Color.blue,Color.green,Color.yellow,Color.red};
		private Vector3 		_scanDirForward = new Vector3 ();
		private Vector3 		_scanDirUp = new Vector3 ();
		private Vector3 		_scanDirRight = new Vector3 ();
		private float 			_reference = 0f;
		private Dictionary<Vector2,float> _radarSamples = new Dictionary<Vector2,float>();
		private Vector2 		_coord = new Vector2 ();
		private bool 			_dirty = true;
		private Vector2 		_maxCoord = new Vector2();
		private Vector2 		_minCoord = new Vector2();
		private double 			_blipTime = 0d;
		private AudioSource 	_audioSource = null;
		private AudioSequence 	_playing = null;


		public override void OnStart (StartState state)
		{
			base.OnStart (state);
			_audioSource = gameObject.AddComponent<AudioSource> ();
			if (blipSound != null && blipSound.sound == null) {
				blipSound = part.partInfo.partPrefab.Modules.GetModules<ModuleTerrainRadar> ()[0].blipSound;
			}
			_audioSource.dopplerLevel = 0.0f;
			_audioSource.panLevel = 0.0f;
			_audioSource.enabled = true;
			_audioSource.Stop ();
			_transform = part.transform;
			if (scanTransform.Length > 0) {
				Transform tempTransform = part.FindModelTransform (scanTransform);
				if (tempTransform != null) {
					_transform = tempTransform;
				}
			}
			GroundReference ();
			_activeRadar = null;
		}
		public override void OnInactive ()
		{
			base.OnInactive ();
			_activeRadar = null;
		}
		public void OnDraw()
		{
			_windowPos = GUILayout.Window (_winID, _windowPos, OnWindow, "Terrain Radar");
		}

		public void OnWindow(int windowID)
		{
			GUILayout.BeginVertical (GUILayout.MinWidth(400.0f),GUILayout.MinHeight(410.0f));
			GUILayout.BeginHorizontal ();
			GUILayout.Box (_terrain,GUILayout.Width(_terrain.width+10));
			GUILayout.Box (_scaleGraph,GUILayout.Width(_scaleGraph.width+10));
			GUILayout.EndHorizontal ();
			GUILayout.Box (_terrainLateral,GUILayout.Width(_terrainLateral.width+10));
			GUILayout.Label ("Max Distance:" + _max.ToString ("F2") + " m");
			GUILayout.Label ("Min Distance:" + _min.ToString ("F2") + " m");
			GUILayout.Label ("Reference Distance:" + _reference.ToString ("F2") + " m");
			GUILayout.BeginHorizontal ();
			GUILayout.Label ("Scale");
			int scaleModeSelect = GUILayout.SelectionGrid (scaleMode, 
				new string[]{ "Auto", "1 m","5 m","10 m", "25 m","50 m" }, 6);
			if (scaleMode != scaleModeSelect) {
				scaleMode = scaleModeSelect;
				switch (scaleMode) {
				case 0:
					_scale = _colorScheme.Count;
					break;
				case 1:
					_scale = 1.0f;
					break;
				case 2:
					_scale = 5f;
					break;
				case 3:
					_scale = 10.0f;
					break;
				case 4:
					_scale = 25.0f;
					break;
				case 5:
					_scale = 50.0f;
					break;
				}
			}
			GUILayout.EndHorizontal ();

			GUILayout.BeginHorizontal ();
			GUILayout.Label ("Reference Mode");
			int referenceModeSelect = GUILayout.SelectionGrid (referenceMode, 
				new string[]{ "Lowest Point", "Sea Level","Center" }, 3);
			if (referenceMode != referenceModeSelect) {
				referenceMode = referenceModeSelect;
				switch (referenceMode) {
				case 0:
					_reference = _max;
					break;
				case 1:
					_reference = (float)vessel.altitude;
					break;
				case 2:
					_reference = 0f;
					break;
				}
			}
			GUILayout.EndHorizontal ();

			GUILayout.BeginHorizontal ();
			GUILayout.Label ("Radius: "+scanRadius.ToString("F0"));
			float radius = GUILayout.HorizontalSlider (scanRadius, 10f, 1000f);
			if (GUILayout.Button ("100 m")) {
				radius = 100f;
			}
			if (radius != scanRadius) {
				scanRadius = (float)Math.Ceiling((double)radius/10)*10;
				_radarSamples.Clear ();
			}
			GUILayout.EndHorizontal ();

			GUILayout.BeginHorizontal ();
			GUILayout.Label ("Orientation:");
			int scanModeSelect = GUILayout.SelectionGrid (scanMode,
				               new string[]{ "Ground", "Part" }, 5);
			if (scanMode != scanModeSelect) {
				scanMode = scanModeSelect;
				switch (scanMode) {
				case 0:
					GroundReference ();
					break;
				case 1:
					PartReference ();
					break;
				}
			}

			GUILayout.EndHorizontal ();
			if (GUILayout.Button ("Close")) {
				ToggleViewRadar ();
			}
			GUILayout.EndVertical ();
			GUI.DragWindow ();
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

		/// <summary>
		/// Use the Part as the orientation reference.
		/// </summary>
		private void PartReference()
		{
			_scanDirForward = _transform.forward;
			_scanDirUp = _transform.up;
			_scanDirRight = _transform.right;
		}

		/// <summary>
		/// Use the ground as the orientation reference.
		/// </summary>
		private void GroundReference()
		{
			if (vessel == null || vessel.mainBody == null) {
				PartReference ();
				return;
			}
			_scanDirForward = -vessel.upAxis;
			_scanDirUp = vessel.mainBody.getRFrmVel(vessel.GetWorldPos3D()).normalized;
			_scanDirRight = -Vector3.Cross (_scanDirForward, _scanDirUp);
		}

		public void Update()
		{
			if (vessel == null)
				return;
			if (_dirty) {
				for (int y = 0; y < _terrain.height; y++) {
					for (int x = 0; x < _terrain.width; x++) {
						_terrain.SetPixel (x, y, Color.black);
					}
				}
				foreach (KeyValuePair<Vector2,float> kvp in _radarSamples) {
					SetPixel (kvp.Key.x, kvp.Key.y, kvp.Value, scanRadius);
				}
				_terrain.Apply ();

				float prevDist = 0f;
				for (int y = 0; y < _scaleGraph.height; y++) {
					float eqDist = _scale * (float)y / _scaleGraph.height;

					for (int x = 0; x < _scaleGraph.width; x++) {
						_scaleGraph.SetPixel (x, y, GetColorForDistance (_reference - eqDist));
					}
					if (Math.Floor (prevDist) != Math.Floor (eqDist)) {
						for (int x = 0; x < _scaleGraph.width / 2; x++) {
							_scaleGraph.SetPixel (x, y, Color.black);
							_scaleGraph.SetPixel (x, y - 1, Color.black);
						}
					}
					prevDist = eqDist;
				}
				_scaleGraph.Apply ();
				for (int y = 0; y < _terrainLateral.height; y++) {
					for (int x = 0; x < _terrainLateral.width; x++) {
						_terrainLateral.SetPixel (x, y, Color.black);
					}
				}
				float radiusInc = scanRadius / 10f;

				Vector2 start = _minCoord.normalized * scanRadius;
				Vector2 end = -_minCoord.normalized * scanRadius;
				for (int x = 0; x < _terrainLateral.width; x++) {
					Vector2 lateralCoord = Vector2.Lerp (start, end, (float)x / _terrainLateral.width);
					foreach (KeyValuePair<Vector2,float> kvp in _radarSamples) {
						if (Math.Abs (kvp.Key.x - lateralCoord.x) < radiusInc &&
						    Math.Abs (kvp.Key.y - lateralCoord.y) < radiusInc) {
							if (kvp.Value < _reference - _scale) {
								_terrainLateral.SetPixel (x, _terrainLateral.height - 1, GetColorForDistance (kvp.Value));
								continue;
							}
							if (kvp.Value > _reference) {
								_terrainLateral.SetPixel (x, 0, GetColorForDistance (kvp.Value));
								continue;
							}
							float adjustedY = -(kvp.Value - _reference);
							int y = (int)(adjustedY * (float)_terrainLateral.height / _scale);
							_terrainLateral.SetPixel (x, y, GetColorForDistance (kvp.Value));
							break;
						}
					}
				}

				_terrainLateral.Apply ();
				_dirty = false;
			}
			double ut = Planetarium.GetUniversalTime ();
			if (audioOutput && vessel.heightFromTerrain > 0f && vessel.heightFromTerrain < 1000f && vessel.situation != Vessel.Situations.LANDED) {
				if (ut - _blipTime > (double)altitudeCurve.Evaluate (vessel.heightFromTerrain) && !_audioSource.isPlaying) {
					if (_activeRadar == this || _activeRadar == null || !_activeRadar.isActiveAndEnabled ||
						_activeRadar.vessel != FlightGlobals.ActiveVessel && vessel == FlightGlobals.ActiveVessel) {
						_activeRadar = this;
						if (!_audioSource.isPlaying && blipSound != null) {
							if (_playing == null) {
								_playing = blipSound;
							}
							if (_playing.sound != null) {
								_audioSource.clip = _playing.sound;
								_audioSource.Play ();
							}
							if (_playing != null) {
								_playing = _playing.next;
							}
						}
					}
					_blipTime = ut;
				}
			} else if (_activeRadar == this) {
				_activeRadar = null;
			}
		}

		public void FixedUpdate()
		{
			if (vessel == null)
				return;
			double ut = Planetarium.GetUniversalTime ();
			if (ut - _prevTime > 1d && _radarVisible) {
				switch (scanMode) {
				case 0:
					GroundReference ();
					break;
				case 1:
					PartReference ();
					break;
				}
				RadarScan (_transform, scanRadius);
			}
		}

		/// <summary>
		/// Sets the radar sample.
		/// </summary>
		/// <param name="x">The x coordinate.</param>
		/// <param name="y">The y coordinate.</param>
		/// <param name="dist">Dist.</param>
		private void SetRadarSample (float x, float y, float dist)
		{
			_coord.x = x;
			_coord.y = y;
			if (_radarSamples.ContainsKey (_coord)) {
				_radarSamples [_coord] = dist;
			} else {
				_radarSamples.Add (new Vector2(x,y), dist);
			}
		}

		/// <summary>
		/// Scan with the Radar.
		/// </summary>
		/// <param name="source">Source.</param>
		/// <param name="radius">Radius.</param>
		private void RadarScan(Transform source, float radius)
		{
			_min = float.NaN;
			_max = float.NaN;

			float radiusInc = radius / 10f;
			float centerDist = RadarBeam (source, 0f, 0f);
			_max = Max (centerDist, _max);
			_min = Min (centerDist, _min);
			_maxCoord.x = 0f;
			_maxCoord.y = 0f;
			_minCoord.x = 0f;
			_minCoord.y = 0f;
			SetRadarSample (0f, 0f, centerDist);

			double segments = 4d;
			int scan = 0;
			for (float dist = radiusInc; dist <= radius; dist += radiusInc) {
				scan++;
				for (double angle = 0.0f; angle < Math.PI * 2; angle += Math.PI / segments) {
					float x = (float)(dist * Math.Cos (angle));
					float y = (float)(dist * Math.Sin (angle));
					float beamDist = RadarBeam (source, x, y);
					float max = Max (beamDist, _max);
					float min = Min (beamDist, _min);
					SetRadarSample (x, y, beamDist);
					if (max != _max) {
						_max = max;
						_maxCoord.x = x;
						_maxCoord.y = y;
					}
					if (min != _min) {
						_min = min;
						_minCoord.x = x;
						_minCoord.y = y;
					}
				}
				if (scan % 3 == 0)
					segments *= 2d;
			}

			if (!float.IsNaN (_min) && !float.IsNaN (_max) && scaleMode == 0) {
				_scale = _max - _min + 1f;
				if (_scale < _colorScheme.Count)
					_scale = _colorScheme.Count;
			}
			if (referenceMode == 0) {
				_reference = _max;
			} else if (referenceMode == 1) {
				_reference = (float)vessel.altitude;
			} else if (referenceMode == 2) {
				_reference = centerDist + _scale / 2f;
				if (_reference < 0f) {
					_reference = 0f;
				}
			}
			_dirty = true;
		}

		/// <summary>
		/// Find the max value ignoring NaN.
		/// </summary>
		/// <param name="lhs">Lhs.</param>
		/// <param name="rhs">Rhs.</param>
		private float Max(float lhs, float rhs)
		{
			if (float.IsNaN(lhs))
				return rhs;
			if (float.IsNaN (rhs))
				return lhs;
			return Math.Max (lhs, rhs);
		}

		/// <summary>
		/// Find the minimum value ignoring NaN.
		/// </summary>
		/// <param name="lhs">Lhs.</param>
		/// <param name="rhs">Rhs.</param>
		private float Min(float lhs, float rhs)
		{
			if (float.IsNaN(lhs))
				return rhs;
			if (float.IsNaN (rhs))
				return lhs;
			return Math.Min (lhs, rhs);
		}


		/// <summary>
		/// Sets the pixel in the terrain radar image.
		/// </summary>
		/// <param name="x">The x coordinate.</param>
		/// <param name="y">The y coordinate.</param>
		/// <param name="value">Value.</param>
		/// <param name="radius">Radius.</param>
		private void SetPixel(float x, float y, float value, float radius)
		{
			int pixelX = (int)(x / radius * _terrain.width / 2 + _terrain.width / 2) ;
			int pixelY = (int)(y / radius * _terrain.height / 2 + _terrain.height / 2);
			Color color = GetColorForDistance (value);

			int pix = 5;
			for (int px = -pix; px <= pix; px++) {
				for (int py = -pix; py <= pix; py++) { 
					if (pixelX + px > _terrain.width)
						continue;
					if (pixelY + py > _terrain.height)
						continue;
					if (pixelX + px < 0)
						continue;
					if (pixelY + py < 0)
						continue;
					_terrain.SetPixel (pixelX + px, pixelY+py, color);
				}
			}
		}

		/// <summary>
		/// Gets the color for a given distance.
		/// </summary>
		/// <returns>The color for distance.</returns>
		/// <param name="dist">Dist.</param>
		private Color GetColorForDistance(float dist)
		{
			if (float.IsNaN (dist) || float.IsNaN(_reference))
				return Color.black;
			float adjusted = -(dist - _reference);

			float scale = _scale / (_colorScheme.Count - 1);
			int idx = (int)Math.Floor(adjusted / scale);
			if (idx > _colorScheme.Count - 2)
				idx = _colorScheme.Count - 2;
			if (idx < 0)
				idx = 0;
			Color a = _colorScheme[idx];
			Color b = _colorScheme[idx+1];

			float scaledDist = adjusted - idx * scale;

			return Color.Lerp (a, b, scaledDist / scale);
		}

		/// <summary>
		/// Sends out one radar beam from the x, y position relative to the source.
		/// </summary>
		/// <returns>The beam.</returns>
		/// <param name="source">Source.</param>
		/// <param name="x">The x coordinate.</param>
		/// <param name="y">The y coordinate.</param>
		private float RadarBeam(Transform source, float x, float y)
		{
			float distance = float.NaN;

			Vector3 forward = _scanDirForward;
			Vector3 position = source.position;
			position += _scanDirUp.normalized * y;
			position += _scanDirRight.normalized * x;
			Ray ray = new Ray (position, forward);
			RaycastHit hit = new RaycastHit ();
			if (Physics.Raycast(ray,out hit,10000.0f,(1<<15)|(1<<4))) {
				distance = hit.distance;
			}
			return distance;
		}

		[KSPAction("Toggle Radar View")]
		public void ToggleRadarViewAction(KSPActionParam param)
		{
			ToggleViewRadar ();
		}

		[KSPAction("Toggle Ping")]
		public void TogglePingAction(KSPActionParam param)
		{
			audioOutput = !audioOutput;
		}
	}
}

