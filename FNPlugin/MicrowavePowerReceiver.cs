﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace FNPlugin {
	class MicrowavePowerReceiver : FNResourceSuppliableModule{
        [KSPField(isPersistant = false, guiActive = true, guiName = "Input Power")]
        public string beamedpower;
        [KSPField(isPersistant = false, guiActive = true, guiName = "Satellites Connected")]
        public string connectedsats;
        [KSPField(isPersistant = false, guiActive = true, guiName = "Total Efficiency")]
        public string toteff;
		[KSPField(isPersistant = true)]
		bool IsEnabled;
        private float powerInput;
        private int connectedsatsf = 0;
        private int activeCount = 0;
        private static int dishcount;
        private int mycount = -1;
        private float totefff;
        private float rangelosses;
        const float angle = 3.64773814E-10f;
        const float efficiency = 0.85f;

		[KSPField(isPersistant = false)]
		public string animName;

		protected Animation anim;

        //protected bool responsible_for_megajoulemanager;
        //protected FNResourceManager megamanager;

		protected bool play_down = true;
		protected bool play_up = true;

		public MicrowavePowerReceiver() : base() {
			String[] resources_to_supply = {FNResourceManager.FNRESOURCE_MEGAJOULES,FNResourceManager.FNRESOURCE_WASTEHEAT};
			this.resources_to_supply = resources_to_supply;
		}

		[KSPEvent(guiActive = true, guiName = "Activate Receiver", active = true)]
		public void ActivateReceiver() {
			IsEnabled = true;
		}

		[KSPEvent(guiActive = true, guiName = "Disable Receiver", active = true)]
		public void DisableReceiver() {
			IsEnabled = false;
		}

		[KSPAction("Activate Receiver")]
		public void ActivateReceiverAction(KSPActionParam param) {
			ActivateReceiver();
		}

		[KSPAction("Disable Receiver")]
		public void DisableReceiverAction(KSPActionParam param) {
			DisableReceiver();
		}

		[KSPAction("Toggle Receiver")]
		public void ToggleReceiverAction(KSPActionParam param) {
			IsEnabled = !IsEnabled;
		}

        public override void OnStart(PartModule.StartState state) {
			Actions["ActivateReceiverAction"].guiName = Events["ActivateReceiver"].guiName = String.Format("Activate Receiver");
			Actions["DisableReceiverAction"].guiName = Events["DisableReceiver"].guiName = String.Format("Disable Receiver");
			Actions["ToggleReceiverAction"].guiName = String.Format("Toggle Receiver");

			base.OnStart (state);
            if (state == StartState.Editor) { return; }
            this.part.force_activate();

			anim = part.FindModelAnimators (animName).FirstOrDefault ();
			if (anim != null) {
				anim [animName].layer = 1;
				if (connectedsatsf > 0) {
					anim [animName].normalizedTime = 1f;
					anim [animName].speed = -1f;

				} else {
					anim [animName].normalizedTime = 0f;
					anim [animName].speed = 1f;

				}
				anim.Play ();
			}

            if (mycount == -1) {
                mycount = dishcount;
                dishcount++;
            }
        }

        public override void OnUpdate() {
			Events["ActivateReceiver"].active = !IsEnabled;
			Events["DisableReceiver"].active = IsEnabled;
			Fields["toteff"].guiActive = (connectedsatsf>0);

			if (IsEnabled) {
				beamedpower = powerInput.ToString () + "KW";
			} else {
				beamedpower = "Offline.";
			}
            connectedsats = connectedsatsf.ToString();
            toteff = totefff.ToString() + "%";

			if (connectedsatsf > 0) {
				if (play_up) {
					play_down = true;
					play_up = false;
					anim [animName].speed = 1f;
					anim [animName].normalizedTime = 0f;
					anim.Blend (animName, 2f);
				}
			} else {
				if (play_down) {
					play_down = false;
					play_up = true;
					anim [animName].speed = -1f;
					anim [animName].normalizedTime = 1f;
					anim.Blend (animName, 2f);
				}

			}
        }

        public override void OnFixedUpdate() {
			base.OnFixedUpdate ();
			                                   
			ConfigNode config = PluginHelper.getPluginSaveFile();
            float powerInputIncr = 0;
            int activeSatsIncr = 0;
            float rangelosses = 0;
			if (config != null && IsEnabled) {
                
				if (activeCount % 100 == 0) {
					List<Vessel> vessels = FlightGlobals.Vessels;
					//print(vessels.Count.ToString() + "\n");
					foreach (Vessel vess in vessels) {
						String vid = vess.id.ToString ();
						//print(vid + "\n");
						if (config.HasValue (vid) && lineOfSightTo (vess) && PluginHelper.lineOfSightToSun (vess)) {
							String powerinputsat = config.GetValue (vid);
							float inputPowerFixedAlt = float.Parse (powerinputsat) * PluginHelper.getSatFloatCurve ().Evaluate ((float)FlightGlobals.Bodies [0].GetAltitude (vess.transform.position));
							float distance = (float)Vector3d.Distance (vessel.transform.position, vess.transform.position);
							float powerdissip = (float)(Math.Tan (angle) * distance * Math.Tan (angle) * distance);
							powerdissip = Math.Max (powerdissip, 1);
							rangelosses += powerdissip;
							powerInputIncr += inputPowerFixedAlt / powerdissip;
							activeSatsIncr++;
						}
					}
					float atmosphericefficiency = (float)Math.Exp (-FlightGlobals.getStaticPressure (vessel.transform.position) / 5);
					this.rangelosses = rangelosses / activeSatsIncr;
					totefff = efficiency * atmosphericefficiency * 100 / rangelosses;
					powerInput = powerInputIncr * efficiency * atmosphericefficiency;
					connectedsatsf = activeSatsIncr;
				}
			} else {
				connectedsatsf = 0;
				powerInput = 0;
			}

            if (powerInput > 1000) {
                float powerInputMegajoules = (powerInput - 1000)/1000;
                float powerInputKilojoules = 1000;
                //part.RequestResource("Megajoules", -powerInputMegajoules * TimeWarp.fixedDeltaTime);
                //megamanager.powerSupply(powerInputMegajoules * TimeWarp.fixedDeltaTime);
				supplyFNResource(powerInputMegajoules * TimeWarp.fixedDeltaTime,FNResourceManager.FNRESOURCE_MEGAJOULES);
				float waste_head_production = powerInput/1000.0f/ efficiency * (1.0f - efficiency);
				supplyFNResource (waste_head_production * TimeWarp.fixedDeltaTime, FNResourceManager.FNRESOURCE_WASTEHEAT);
                part.RequestResource("ElectricCharge", -powerInputKilojoules * TimeWarp.fixedDeltaTime);
            }
            else {
				float waste_head_production = powerInput/1000.0f/efficiency * (1.0f - efficiency);
				supplyFNResource (waste_head_production * TimeWarp.fixedDeltaTime, FNResourceManager.FNRESOURCE_WASTEHEAT);
                part.RequestResource("ElectricCharge", -powerInput * TimeWarp.fixedDeltaTime);
            }
            activeCount++;
        }

        protected bool lineOfSightTo(Vessel vess) {
            Vector3d a = vessel.transform.position;
            Vector3d b = vess.transform.position;
            foreach (CelestialBody referenceBody in FlightGlobals.Bodies) {
                Vector3d refminusa = referenceBody.position - a;
                Vector3d bminusa = b - a;
                if (Vector3d.Dot(refminusa, bminusa) > 0) {
                    if (Vector3d.Dot(refminusa, bminusa.normalized) < bminusa.magnitude) {
                        Vector3d tang = refminusa - Vector3d.Dot(refminusa, bminusa.normalized) * bminusa.normalized;
                        if (tang.magnitude < referenceBody.Radius) {
                            return false;
                        }
                    }
                }
            }
            return true;
        }
    }

    
}
