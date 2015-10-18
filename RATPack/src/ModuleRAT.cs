/*
 * This file is subject to the included LICENSE.md file. 
 */

using System;
using System.Collections.Generic;
using UnityEngine;

namespace RATPack
{
	public class ModuleRAT: PartModule
	{
		[KSPField]
		public float minDensity = 0.001f;

		[KSPField]
		public float chargeRate = 1.0f;

		[KSPField]
		public string generatorAnimation = "Generate";

		[KSPField]
		public string deployAnimation = "Deploy";

		[KSPField(isPersistant=true,guiActive=true,guiName="Active")]
		public bool deployed = false;

		[KSPField]
		public bool autoDeploy = true;

		[KSPField]
		public bool managePartCharge = false;

		[KSPField(guiActive=true,guiName="Charge Flow",guiFormat="F4")]
		public float chargePerSec = 1.0f;

		[KSPField]
		public float generatorAnimationSpeed = 5.0f;

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

		private double 			_lastTime = 0.0d;
		private bool 			_deploying = false;
		private AnimationState 	_genAnim = null;
		private AnimationState 	_deployAnim = null;
		private Animation 		_animation = null;
		private Part			_chargeProvider = null;
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
				Events ["ToggleDeploy"].guiName = "Deactivate";
			}
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

			// Use indicated airspeed. It takes air density into account which is what we want.
			double curveFit = (double)airspeedCurve.Evaluate ((float)vessel.indicatedAirSpeed);
			chargePerSec = 0.0f;

			if (curveFit > 0.0f && !part.ShieldedFromAirstream) {
				if (deployed) {
					if (_animation != null && _genAnim != null) {
						if (!_animation.isPlaying) {
							_animation.Play (generatorAnimation);
						} else {
							_genAnim.speed = 1.0f + (float)curveFit * generatorAnimationSpeed;
						}
					}
					chargePerSec = (float)(chargeRate * curveFit);
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
			Events ["ToggleDeploy"].guiName = "Deactivate";
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

			Events ["ToggleDeploy"].guiName = "Activate";
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
		[KSPEvent(guiActive=true,guiName="Activate",unfocusedRange=5f,guiActiveUnfocused=true,guiActiveEditor=true)]
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

