/*
 * Copyright 2015 SatNet
 * 
 * This file is subject to the included LICENSE.md file. 
 */

using KSP.Localization;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RATPack
{
    public class ModuleThrustReverse : PartModule
    {

        [KSPField]
        public string deployAnimation = "#LOC_RAT_4";

        [KSPField(isPersistant = true, guiActive = true, guiName = "Active")]
        public bool deployed = false;

        [KSPField(guiActive = true, guiName = "Thrust", guiFormat = "F3")]
        public float effectiveThrust = 0.0f;

        [KSPField]
        public string thrustTransformName = "#LOC_RAT_64";

        [KSPField]
        public float thrustModifier = 0.5f;

        [KSPField]
        public string reversingEffect = "#LOC_RAT_65";

        [KSPField(guiActive = true, guiName = "Engine", guiActiveEditor = true)]
        public string engineName = "#LOC_RAT_66";

        private AnimationState _deployAnim = null;
        private Animation _animation = null;
        private Part _engine = null;
        private bool _exhaustDamage = false;
        private bool _exhaustFix = false;
        private List<ParticleSystem> _emitList = new List<ParticleSystem>();
        /// <summary>
        /// Called when the flight starts, or when the part is created in the editor. OnStart will be called
        ///  before OnUpdate or OnFixedUpdate are ever called.
        /// </summary>
        /// <param name="state">Some information about what situation the vessel is starting in.</param>
        public override void OnStart(StartState state)
        {
            _animation = part.FindModelComponent<Animation>();
            if (_animation != null && deployAnimation.Length > 0)
            {
                _deployAnim = _animation[deployAnimation];
            }

            UpdateEngineLink();
            part.Effect(reversingEffect, 0.0f);
            if (deployed)
            {
                Activate();
            }

            Transform[] transforms = part.FindModelTransforms(thrustTransformName);
            Debug.Log("ThrustTransforms:" + transforms.Length);

            if (state == StartState.Editor)
                GameEvents.onEditorShipModified.Add(EditorUpdate);
        }

        public override void OnInactive()
        {
            if (HighLogic.LoadedScene == GameScenes.EDITOR)
                GameEvents.onEditorShipModified.Remove(EditorUpdate);

        }

        /// <summary>
        /// Updates the engine link.
        /// </summary>
        private void UpdateEngineLink()
        {
            _engine = null;
            engineName = Localizer.Format("#LOC_RAT_66");
            foreach (Part child in part.children)
            {
                foreach (PartModule pm in child.Modules)
                {
                    if (pm is ModuleEngines)
                    {
                        ModuleEngines eng = (ModuleEngines)pm;
                        if (eng != null)
                        {
                            _engine = child;
                            _exhaustDamage = eng.exhaustDamage;
                            engineName = _engine.partInfo.title;
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Callback when the ship is updated in the editor.
        /// </summary>
        /// <param name="ship">Ship.</param>
        private void EditorUpdate(ShipConstruct ship)
        {
            UpdateEngineLink();
        }

        /// <summary>
        /// Called on physics update. Add the appropriate amount of charge.
        /// </summary>
        public void FixedUpdate()
        {
            if (vessel != null && deployed && _engine != null && vessel.parts.Contains(_engine))
            {
                float thrust = 0.0f;
                float maxThrust = 0.0f;
                foreach (PartModule pm in _engine.Modules)
                {
                    if (pm is ModuleEngines)
                    {
                        ModuleEngines eng = (ModuleEngines)pm;
                        if (eng != null && !eng.flameout)
                        {
                            thrust += eng.resultingThrust;
                            maxThrust += eng.maxThrust;
                        }
                    }
                }
                foreach (ParticleSystem emit in _emitList)
                {
                    //                    emit.enableEmission = false;
                    var e = emit.emission;
                    e.enabled = false;

                    emit.Clear();
                }
                if (thrust > 0.0f)
                {
                    part.Effect(reversingEffect, thrust / maxThrust * thrustModifier);
                }
                else
                {
                    part.Effect(reversingEffect, 0.0f);
                }

                Transform[] transforms = part.FindModelTransforms(thrustTransformName);
                float thrustPerTransform = thrust / transforms.Length;
                Vector3 thrustResult = new Vector3();
                foreach (Transform output in transforms)
                {
                    thrustResult += output.forward.normalized * -thrustPerTransform * thrustModifier;
                    this.part.Rigidbody.AddForceAtPosition(output.forward.normalized * -thrustPerTransform * thrustModifier, output.position);
                }

                effectiveThrust = thrustResult.magnitude;

                // Counter the engine thrust.
                foreach (PartModule pm in _engine.Modules)
                {
                    if (pm is ModuleEngines)
                    {
                        ModuleEngines eng = (ModuleEngines)pm;
                        if (eng != null && eng.resultingThrust != 0.0f && !eng.flameout)
                        {
                            List<Transform> engTrans = eng.thrustTransforms;
                            float thrustPerNozzle = eng.resultingThrust / engTrans.Count;
                            foreach (Transform engOutput in engTrans)
                            {
                                _engine.Rigidbody.AddForceAtPosition(engOutput.forward.normalized * thrustPerNozzle, engOutput.position);
                            }
                        }
                    }
                }
            }
            else
            {
                part.Effect(reversingEffect, 0.0f);
                effectiveThrust = 0.0f;
                if (!_animation.isPlaying && _exhaustFix)
                {
                    foreach (PartModule pm in _engine.Modules)
                    {
                        if (pm is ModuleEngines)
                        {
                            ModuleEngines eng = (ModuleEngines)pm;
                            if (eng != null)
                            {
                                eng.exhaustDamage = _exhaustDamage;
                                _exhaustFix = false;
                            }
                        }
                    }
                }
            }

        }

        /// <summary>
        /// Activate the thrust reverser.
        /// </summary>
        public void Activate()
        {
            List<string> runningEffect = new List<string>();
            _emitList.Clear();
            foreach (PartModule pm in _engine.Modules)
            {
                if (pm is ModuleEngines)
                {
                    ModuleEngines eng = (ModuleEngines)pm;
                    if (eng != null)
                    {
                        eng.exhaustDamage = false;
                    }
                    if (eng is ModuleEnginesFX)
                    {
                        ModuleEnginesFX engFx = (ModuleEnginesFX)eng;
                        if (engFx != null)
                        {
                            if (engFx.runningEffectName.Length > 0)
                                runningEffect.Add(engFx.runningEffectName);
                            if (engFx.powerEffectName.Length > 0)
                                runningEffect.Add(engFx.powerEffectName);
                            EffectBehaviour[] fxList = _engine.GetComponents<EffectBehaviour>();
                            if (fxList.Length == 0)
                                fxList = _engine.GetComponentsInChildren<EffectBehaviour>();
                            foreach (EffectBehaviour fx in fxList)
                            {
                                if (runningEffect.Contains(fx.effectName))
                                {
                                    ParticleSystem[] emitArray = fx.GetComponentsInChildren<ParticleSystem>();
                                    _emitList.AddRange(emitArray);
                                }
                            }
                        }
                    }
                    else
                    {
                        ParticleSystem[] emitArray = _engine.GetComponentsInChildren<ParticleSystem>();
                        _emitList.AddRange(emitArray);
                    }
                }
            }

            foreach (ParticleSystem emit in _emitList)
            {
                var e = emit.emission;
                e.enabled = false;
                //emit.enableEmission = false;
                emit.Clear();
            }

            _deployAnim.speed = 1.0f;
            _animation.Play();
            deployed = true;
            Events["ToggleDeploy"].guiName = "Deactivate";

        }

        /// <summary>
        /// Deactivate the thrust reverser.
        /// </summary>
        public void Deactivate()
        {
            _deployAnim.time = _deployAnim.length;
            _deployAnim.speed = -1.0f;
            _animation.Play();
            _exhaustFix = true;
            List<string> runningEffect = new List<string>();
            _emitList.Clear();
            foreach (PartModule pm in _engine.Modules)
            {
                if (pm is ModuleEngines)
                {
                    ModuleEngines eng = (ModuleEngines)pm;
                    if (eng is ModuleEnginesFX)
                    {
                        ModuleEnginesFX engFx = (ModuleEnginesFX)eng;
                        if (engFx != null)
                        {
                            if (engFx.runningEffectName.Length > 0)
                                runningEffect.Remove(engFx.runningEffectName);
                            if (engFx.powerEffectName.Length > 0)
                                runningEffect.Remove(engFx.powerEffectName);
                            EffectBehaviour[] fxList = _engine.GetComponents<EffectBehaviour>();
                            if (fxList.Length == 0)
                                fxList = _engine.GetComponentsInChildren<EffectBehaviour>();
                            foreach (EffectBehaviour fx in fxList)
                            {
                                if (runningEffect.Contains(fx.effectName))
                                {
                                    ParticleSystem[] emitArray = fx.GetComponentsInChildren<ParticleSystem>();
                                    _emitList.AddRange(emitArray);
                                }
                            }
                        }
                    }
                    else
                    {
                        //ParticleSystem[] emitArray = _engine.GetComponentsInChildren<ParticleSystem>();
                        _emitList.Clear();// RemoveRange(emitArray);
                    }
                }
            }

            foreach (ParticleSystem emit in _emitList)
            {
                //emit.enableEmission = true;
                var e = emit.emission;
                e.enabled = true;
                emit.Clear();
            }

            part.Effect(reversingEffect, 0.0f);

            deployed = false;
            Events["ToggleDeploy"].guiName = "Activate";
        }

        /// <summary>
        /// Activate/deactivates the thrust reverser.
        /// </summary>
        [KSPEvent(guiActive = true, guiName = "Activate", unfocusedRange = 5f, guiActiveUnfocused = true, guiActiveEditor = true)]
        public void ToggleDeploy()
        {
            if (!deployed)
            {
                Activate();
            }
            else
            {
                Deactivate();
            }
        }

        /// <summary>
        /// Action to toggles the thrust reverser.
        /// </summary>
        /// <param name="param">Parameter.</param>
        [KSPAction("Toggle")]
        public void ToggleTRAction(KSPActionParam param)
        {
            ToggleDeploy();
        }

        /// <summary>
        /// Activates the thrust reverser.
        /// </summary>
        /// <param name="param">Parameter.</param>
        [KSPAction("Activate")]
        public void DeployTRAction(KSPActionParam param)
        {
            Activate();
        }

        /// <summary>
        /// Action to deactivate the thrust reverser.
        /// </summary>
        /// <param name="param">Parameter.</param>
        [KSPAction("Deactivate")]
        public void ResetTRAction(KSPActionParam param)
        {
            Deactivate();
        }
    }
}

