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
	public class ModuleRAT: PartModule
	{
		const float MAX_CHARGE_RATE = 5f;
		const float MAX_AIRSPEED = 2000f;
		const double GRAPH_UPDATE_TIME = 2.0d;

		[KSPField]
		public float minDensity = 0.001f;

		[KSPField]
		public float chargeRate = 1.0f;

		[KSPField]
		public string generatorAnimation = "Generate";

		[KSPField]
		public string deployAnimation = "Deploy";

		[KSPField(isPersistant=true,guiActive=true,guiName="RAT Active")]
		public bool deployed = false;

		[KSPField]
		public bool autoDeploy = true;

		[KSPField]
		public bool managePartCharge = false;

		[KSPField(guiActive=true,guiName="Charge Flow",guiFormat="F4")]
		public float chargePerSec = 1.0f;

		[KSPField(guiActive=true,guiName="Blocked",guiActiveEditor=true)]
		public string blocked="False";

		[KSPField]
		public float generatorAnimationSpeed = 5.0f;

		[KSPField]
		public string transformName = "";

		/// <summary>
		/// The airspeed curve. Determines how much charge we get at a given indicated airspeed.
		/// </summary>
		[KSPField]
		public FloatCurve airspeedCurve = new FloatCurve(new Keyframe[]
			{
				new Keyframe(0.0f,0.0f,0.0f,0.0f),
				new Keyframe(1.0f,0.1f,0.0f,0.0f),
				new Keyframe(100.0f,1.0f,0.0f,0.0f),
				new Keyframe(1600.0f,0.1f,0.0f,0.0f)
			});
		/// <summary>
		/// The atmosphere curve. Determines how much we change the charge based on atmosphere.
		/// </summary>
		[KSPField]
		public FloatCurve atmosphereCurve = new FloatCurve(new Keyframe[]
			{
				new Keyframe(0.0f,0.0f,0.0f,0.0f),
				new Keyframe(0.00005f,0.0f,0.0f,0.0f),
				new Keyframe(0.1f,0.3f,0.0f,1.2f),
				new Keyframe(1.0f,1.0f,0.0f,0.0f),
			});

		private double 			_lastTime = 0.0d;
		private bool 			_deploying = false;
		private AnimationState 	_genAnim = null;
		private AnimationState 	_deployAnim = null;
		private Animation 		_animation = null;
		private Part			_chargeProvider = null;
		private List<Transform> _chargeTransform = new List<Transform>();
		private Rect 			_windowPos = new Rect();
		private bool 			_powerCurveView = false;
		private bool 			_maxPowerCurveScale = true;
		private Graph 			_powerGraph 			= new Graph(500,400);
		private int 			_winID = 1;
		private double			_partSpeed = 0.0f;
		private double			_graphUpdateTime = 0.0d;
		/// <summary>
		/// Called when the flight starts, or when the part is created in the editor. OnStart will be called
		///  before OnUpdate or OnFixedUpdate are ever called.
		/// </summary>
		/// <param name="state">Some information about what situation the vessel is starting in.</param>
		public override void OnStart(StartState state)
		{
			_animation = part.FindModelComponent<Animation>();
			if (_animation != null && generatorAnimation.Length > 0) {
				_genAnim = _animation[generatorAnimation];
			}
			if (_animation != null && deployAnimation.Length > 0) {
				_deployAnim = _animation [deployAnimation];
			}
			if (deployed && _deployAnim != null) {
				_animation.Play (deployAnimation);
				Events ["ToggleDeploy"].guiName = "Deactivate RAT";
			}
			_winID = GUIUtility.GetControlID (FocusType.Passive);

			if (transformName.Length > 0)
				_chargeTransform.AddRange(part.FindModelTransforms (transformName));

			if (_chargeTransform.Count == 0)
				_chargeTransform.Add (part.transform);
		}
		/// <summary>
		/// Called on physics update. Add the appropriate amount of charge.
		/// </summary>
		public void FixedUpdate()
		{
			// If we are deploying check to see if the animation is finished yet.
			if (_deploying && _deployAnim != null) {
				if (!_animation.IsPlaying (deployAnimation)) {
					deployed = true;
					_deploying = false;
				}
			}

			if (vessel == null) {
				// Animate while in the editor.
				if (deployed && _animation != null && _genAnim != null) {
					if (!_animation.isPlaying) {
						_genAnim.speed = generatorAnimationSpeed;
						_animation.Play (generatorAnimation);
					}
				}
				OcclusionFactor ();
				return;
			}
			if (managePartCharge) {
				foreach (PartResource res in part.Resources) {
					if (res.info.name == "ElectricCharge" && res.amount == res.maxAmount && HasElectricCharge()) {
						res.flowState = false;
					}
				}
			}

			double time = Planetarium.GetUniversalTime ();
			double deltaTime = time - _lastTime;
			_lastTime = time;

			// Take the vessel speed then account for orientation, pressure, and occlusion.
			double partSpeed = vessel.speed;
			double orientationFactorTotal = 0d;
			double orientationFactorCount = 0d;
			double orientationFactor = 1d;
			foreach (Transform trans in _chargeTransform) {
				orientationFactorCount++;
				double speedComponent = Vector3d.Dot (vessel.srf_velocity.normalized, trans.up.normalized);
				if (speedComponent > 0.0d)
					orientationFactorTotal += speedComponent;
				else
					orientationFactorTotal += -speedComponent * 0.5;
			}
			if (orientationFactorCount > 0d) {
				orientationFactor = orientationFactorTotal / orientationFactorCount;
			}
			double occlusionFactor = OcclusionFactor ();

			double atmoCurveFit = (double)atmosphereCurve.Evaluate ((float)vessel.atmDensity);

			partSpeed *= orientationFactor;
			partSpeed *= atmoCurveFit;
			_partSpeed = partSpeed;

			if (time - _graphUpdateTime > GRAPH_UPDATE_TIME) {
				DrawPowerGraph ();
				_graphUpdateTime = time;
			}

			double curveFit = (double)airspeedCurve.Evaluate ((float)partSpeed);

			chargePerSec = 0.0f;

			if (curveFit > 0.0f && atmoCurveFit > 0.0f && occlusionFactor > 0.0f) {
				if (deployed) {
					double chargePct = curveFit * occlusionFactor;
					if (_animation != null && _genAnim != null) {
						if (!_animation.isPlaying) {
							_animation.Play (generatorAnimation);
						} else {
							_genAnim.speed = (float)chargePct * generatorAnimationSpeed;
						}
					}
					chargePerSec = (float)(chargeRate * chargePct);
					if (chargePerSec > 0.0f) {
						double charge = chargePerSec * (deltaTime);
						part.RequestResource ("ElectricCharge", -charge);
					}
				} else if (!_deploying && autoDeploy && vessel.atmDensity > minDensity && !HasElectricCharge ()) {
					Debug.Log ("AutoDeploy RAT");
					DeployRAT ();
					foreach (Part sym in part.symmetryCounterparts) {
						ModuleRAT rat = sym.FindModuleImplementing<ModuleRAT> ();
						if (rat != null)
							rat.DeployRAT ();
					}
				}
			} else {
				if (_animation!= null && _animation.IsPlaying(generatorAnimation))
					_animation.Stop ();
			}
		}

		/// <summary>
		/// Calculates the occlusion factor.
		/// </summary>
		/// <returns>The factor.</returns>
		private double OcclusionFactor()
		{
			double factor = 1.0d;
			double total = 0.0d;
			double count = 0.0d;
			string hitobj = "";
			foreach (Transform trans in _chargeTransform) {
				Vector3 origin = trans.position;
				if (trans == part.transform) {
					origin += trans.up * part.collider.bounds.size.magnitude;
				}
				Ray ray = new Ray (origin, trans.up);
				RaycastHit hit = new RaycastHit ();
				if (Physics.Raycast (ray, out hit, 2.0f)) {
					total += (double)(hit.distance / 2.0f);
					count++;
					if (hit.rigidbody != null) {
						hitobj = hit.rigidbody.name;
					}
				}
			}

			if (count > 0d) {
				factor = Math.Log10(total / count * 9.0d + 1.0d);
				blocked = ((1.0f - factor) * 100.0f).ToString("F2") + "% by " + hitobj;
			} else {
				blocked = "False";
			}

			return factor;
		}

		/// <summary>
		/// Lock the Controls.
		/// </summary>
		private void ControlLock()
		{
			InputLockManager.SetControlLock (ControlTypes.EDITOR_SOFT_LOCK, "ModuleRAT");
		}

		/// <summary>
		/// Unlock the Controls.
		/// </summary>
		private void ControlUnlock()
		{
			InputLockManager.RemoveControlLock("ModuleRAT");
		}

		public void OnDraw()
		{
			_windowPos = GUILayout.Window (_winID, _windowPos, OnWindow, part.partInfo.title);
			if (_windowPos.Contains (Event.current.mousePosition)) {
				ControlLock ();
			} else {
				ControlUnlock();
			}
		}

		/// <summary>
		/// Draws the window.
		/// </summary>
		/// <param name="windowID">Window ID</param>
		public void OnWindow(int windowID)
		{
			GUILayout.BeginVertical (GUILayout.Width(500.0f),GUILayout.Height(410.0f));
			GUILayout.Label ("Electric Charge / second vs. Airspeed");
			GUILayout.Box (_powerGraph.getImage());
			GUILayout.Label ("Vertical Ticks - 1 EC/s");
			GUILayout.Label ("Horizontal Ticks - 100 m/s");
			if (vessel)
				GUILayout.Label ("Atmosphere Compensation Factor:"+atmosphereCurve.Evaluate ((float)vessel.atmDensity).ToString("F3"));
			if (_maxPowerCurveScale) {
				GUILayout.Label ("Showing common scale. Suitable for comparing RATs.");
			} else {
				GUILayout.Label ("Showing fit scale. Minimum for showing this one RAT.");
			}
			GUILayout.BeginHorizontal ();

			if (GUILayout.Button ("Close")) {
				TogglePowerCurve ();
			}
			if (GUILayout.Button (_maxPowerCurveScale ? "Fit Scale" : "Common Scale")) {
				_maxPowerCurveScale = !_maxPowerCurveScale;
				DrawPowerGraph ();
			}
			GUILayout.EndHorizontal ();
			GUILayout.EndVertical ();
			GUI.DragWindow ();
		}

		/// <summary>
		/// Draws the power graph.
		/// </summary>
		public void DrawPowerGraph()
		{
			float max = airspeedCurve.maxTime;
			float min = airspeedCurve.minTime;
			float amin = 0.0f;
			float amax = 0.0f;
			float tmin = 0.0f;
			float tmax = 0.0f;
			airspeedCurve.FindMinMaxValue (out amin, out amax, out tmin, out tmax);

			float chargeMax = chargeRate * amax;

			if (_maxPowerCurveScale) {
				max = MAX_AIRSPEED;
				chargeMax = MAX_CHARGE_RATE;
			}

			float increment = max / _powerGraph.getImage ().width;
			float chargeInc = (float) _powerGraph.getImage ().height / chargeMax;
			_powerGraph.reset ();
			_powerGraph.drawLineOnGraph (x => (double)(airspeedCurve.Evaluate((float)x * increment)*chargeRate), chargeMax, amin, _powerGraph.getImage().width, Color.red  );
			for (float vert = 0.0f; vert < max; vert += 100) {
				int pos = (int)(vert / increment);
				_powerGraph.drawVerticalLine (pos, Color.yellow, 10);
			}
			_powerGraph.drawVerticalLine ((int)(_partSpeed / increment), Color.green);
			for (float horiz = 0.0f; horiz < chargeMax; horiz++) {
				int pos = (int)(horiz * chargeInc);
				_powerGraph.drawHorizontalLine (pos, Color.cyan, 10);
			}
			_powerGraph.Apply ();
		}


		[KSPEvent(guiActive=true,guiName="Power Curve Graph",guiActiveEditor=true)]
		public void TogglePowerCurve()
		{
			DrawPowerGraph ();
			_powerCurveView = !_powerCurveView;
			if (_powerCurveView) {
				RenderingManager.AddToPostDrawQueue (0, OnDraw);
			} else {
				RenderingManager.RemoveFromPostDrawQueue (0, OnDraw);
				ControlUnlock ();
			}

		}

		/// <summary>
		/// Determines whether this vessel has electric charge.
		/// </summary>
		/// <returns><c>true</c> if this vessel has electric charge; otherwise, <c>false</c>.</returns>
		private bool HasElectricCharge()
		{
			// Find a part that supplies power. We'll cache the first part that has electric charge so we don't have to look
			// at every part every time this is called.
			if (_chargeProvider == null) {
				foreach (Part vpart in vessel.parts) {
					foreach (PartResource res in vpart.Resources) {
						if (res.info.name == "ElectricCharge")
						{
							if (res.flowState && res.amount > 0.1f) {
								_chargeProvider = vpart;
								return true;
							}
							break;
						}
					}
				}
			}
			// If we have a cached charge provider check if it still has charge.
			if (_chargeProvider != null && vessel.parts.Contains (_chargeProvider)) {
				foreach (PartResource res in _chargeProvider.Resources) {
					if (res.info.name == "ElectricCharge") {
						if (res.flowState && res.amount > 0.1f) {
							return true;
						} else {
							_chargeProvider = null;
						}
					}
				}
			} else {
				_chargeProvider = null;
			}

			// The charge provider was either not present or had no charge. Request and return charge. If anything responds
			// we know we have power. We'll find a new charge provider on the next call.
			bool result = false;
			double avail = part.RequestResource ("ElectricCharge", 0.1f);
			if (avail > 0.0f) {
				result = true;
				part.RequestResource("ElectricCharge", -avail);
			}
			return result;
		}

		/// <summary>
		/// Deploy the RAT.
		/// </summary>
		public void DeployRAT ()
		{
			Debug.Log ("Deploy RAT");

			if (_animation != null && _deployAnim != null) {
				_deployAnim.speed = 1.0f;
				_animation.Play (deployAnimation);
				_deploying = true;
			} else {
				deployed = true;
			}
			if (managePartCharge) {
				foreach (PartResource res in part.Resources) {
					if (res.info.name == "ElectricCharge") {
						res.flowState = true;
					}
				}
			}
			Events ["ToggleDeploy"].guiName = "Deactivate RAT";
		}

		/// <summary>
		/// Resets the RAT.
		/// </summary>
		public void ResetRAT()
		{
			if (_animation!= null && _animation.isPlaying)
				_animation.Stop ();
			if (_animation != null && _deployAnim != null) {
				_deployAnim.time = _deployAnim.length;
				_deployAnim.speed = -1.5f;
				_animation.Play (deployAnimation);
			}
			_deploying = false;
			deployed = false;

			Events ["ToggleDeploy"].guiName = "Activate RAT";
		}

		/// <summary>
		/// Gets the description for this part.
		/// </summary>
		/// <returns>The info.</returns>
		public override string GetInfo ()
		{
			float min = 0.0f;
			float max = 0.0f;
			float tmin = 0.0f;
			float tmax = 0.0f;
			airspeedCurve.FindMinMaxValue (out min, out max, out tmin, out tmax);

			return "Charge Rate: "+chargeRate+"/sec (Max)\n"+
				"Activates: "+ (autoDeploy ? "Automatically" : "Manually")+"\n"+
				"Peak Charging@ "+tmax+" m/s (ASL)\n"+
				(managePartCharge ? "Keeps local power in reserve." : "");
		}

		/// <summary>
		/// Activate/deactivates the RAT.
		/// </summary>
		[KSPEvent(guiActive=true,guiName="Activate RAT",unfocusedRange=5f,guiActiveUnfocused=true,guiActiveEditor=true)]
		public void ToggleDeploy()
		{
			if (deployed || _deploying)
				ResetRAT ();
			else
				DeployRAT ();
		}

		/// <summary>
		/// Action to toggles the RAT.
		/// </summary>
		/// <param name="param">Parameter.</param>
		[KSPAction("Toggle RAT")]
		public void ToggleRATAction(KSPActionParam param)
		{
			ToggleDeploy ();
		}

		/// <summary>
		/// Activates the RAT.
		/// </summary>
		/// <param name="param">Parameter.</param>
		[KSPAction("Activate RAT")]
		public void DeployRATAction(KSPActionParam param)
		{
			DeployRAT ();
		}

		/// <summary>
		/// Action to deactivate the RAT.
		/// </summary>
		/// <param name="param">Parameter.</param>
		[KSPAction("Deactivate RAT")]
		public void ResetRATAction(KSPActionParam param)
		{
			ResetRAT ();
		}
	}
}

