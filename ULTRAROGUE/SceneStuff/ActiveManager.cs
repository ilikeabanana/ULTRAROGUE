using SettingsMenu.Components.Pages;
using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using Ultrarogue.Items;
using Ultrarogue.Thorn_Modules;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Ultrarogue.SceneStuff
{
    [ConfigureSingleton(SingletonFlags.DestroyDuplicates)]
    public class ActiveManager : MonoSingleton<ActiveManager>
    {
        public Slider ChargeMeter;
        public GameObject AltChargeMeter;
        public Image AltChargeMeterMeter;
        public Image CurrentActiveItemImage;

        Dictionary<ActiveItem, int> charges = new Dictionary<ActiveItem, int>();
        public void Charge()
        {
            int charge = charges[CurrentActive];
            if (charge >= CurrentActive.ChargeRequired) return;
            charges[CurrentActive]++;
            if (ChargeMeter != null)
            {
                ApplyCharge(charge + 1);
            }
        }

        ActiveItem _current;

        public ActiveItem CurrentActive
        {
            get
            {
                return _current;
            }
            set
            {
                if (!charges.ContainsKey(value))
                {
                    charges.Add(value, value.ChargeRequired);
                }
                _current = value;

                if (ChargeMeter != null)
                {


                    ChargeMeter.minValue = 0;
                    ChargeMeter.maxValue = _current.ChargeRequired;
                    ApplyCharge(charges[value]);
                }

                if(CurrentActiveItemImage != null)
                {
                    CurrentActiveItemImage.sprite = value.ItemIcon;
                }

                if (HUDSettings.weaponIconEnabled)
                {

                    ChargeMeter.gameObject.SetActive(true);
                }
                else
                {
                    AltChargeMeter.SetActive(true);
                }

                CurrentActiveItemImage.gameObject.SetActive(true);
            }
        }

        public void ApplyCharge(int charge)
        {
            if (HUDSettings.weaponIconEnabled)
            {
                ChargeMeter.value = charge;
            }
            else
            {
                AltChargeMeterMeter.fillAmount = (float)charge / (float)_current.ChargeRequired;
                if(AltChargeMeterMeter.fillAmount >= 0.99f)
                {
                    AltChargeMeterMeter.color = Color.cyan;
                }
                else
                {
                    AltChargeMeterMeter.color = Color.red;
                }
            }
        }

        void Start()
        {
            CurrentActiveItemImage.GetComponentInChildren<TMP_Text>().text =
                SettingsModule.ActiveKeyCode.Value.ToString();
        }
        void Update()
        {
            if (CurrentActive == null) return;
            int charge = charges[CurrentActive];
            if (CurrentActive.ChargeRequired != charge)
            {
                CurrentActiveItemImage.color = Color.grey;
                return;
            }
            CurrentActiveItemImage.color = Color.white;
            if (CurrentActive == null) return;
            if ((Input.GetKeyDown(SettingsModule.ActiveKeyCode.Value) || (CurrentActive.CanAutoActivate() && SettingsModule.AutoActive.Value)) && GunControl.Instance.activated)
            {

                charges[CurrentActive] = 0;
                CurrentActive?.OnUse();
                if (ChargeMeter != null)
                {
                    ApplyCharge(0);
                }
            }
        }
    }
}
