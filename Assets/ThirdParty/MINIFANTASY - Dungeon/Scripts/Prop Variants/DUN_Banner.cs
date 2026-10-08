using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Minifantasy.Dungeon
{
    public class DUN_Banner : MonoBehaviour
    {
        [Tooltip("Select a Banner.")]
        [SerializeField] private bool damaged = false;
        [SerializeField] private DamagedSelection damagedSelection = DamagedSelection.Damaged0;

        [Header("Sprites")]
        [SerializeField] private Sprite banner;
        [SerializeField] private Sprite damaged_banner_0;
        [SerializeField] private Sprite damaged_banner_1;
        [SerializeField] private Sprite damaged_banner_2;
        [SerializeField] private Sprite damaged_banner_3;
        [SerializeField] private Sprite damaged_banner_4;

        [Header("Shadows")]
        [SerializeField] private Sprite banner_shadow;
        [SerializeField] private Sprite damaged_banner_0_shadow;
        [SerializeField] private Sprite damaged_banner_1_shadow;
        [SerializeField] private Sprite damaged_banner_2_shadow;
        [SerializeField] private Sprite damaged_banner_3_shadow;
        [SerializeField] private Sprite damaged_banner_4_shadow;

        private void OnValidate()
        {
            Sprite selectedSprite = null;
            Sprite selectedShadow = null;

            if (!damaged)
            {
                selectedSprite = banner;
                selectedShadow = banner_shadow;
            }
            else
            {
                switch (damagedSelection)
                {
                    case DamagedSelection.Damaged0:
                        selectedSprite = damaged_banner_0;
                        selectedShadow = damaged_banner_0_shadow;
                        break;
                    case DamagedSelection.Damaged1:
                        selectedSprite = damaged_banner_1;
                        selectedShadow = damaged_banner_1_shadow;
                        break;
                    case DamagedSelection.Damaged2:
                        selectedSprite = damaged_banner_2;
                        selectedShadow = damaged_banner_2_shadow;
                        break;
                    case DamagedSelection.Damaged3:
                        selectedSprite = damaged_banner_3;
                        selectedShadow = damaged_banner_3_shadow;
                        break;
                    case DamagedSelection.Damaged4:
                        selectedSprite = damaged_banner_4;
                        selectedShadow = damaged_banner_4_shadow;
                        break;
                }
            }

            GetComponent<SpriteRenderer>().sprite = selectedSprite;
            transform.Find("Shadow").GetComponent<SpriteRenderer>().sprite = selectedShadow;
        }

        private enum DamagedSelection
        {
            Damaged0,
            Damaged1,
            Damaged2,
            Damaged3,
            Damaged4,
        }
    }
}