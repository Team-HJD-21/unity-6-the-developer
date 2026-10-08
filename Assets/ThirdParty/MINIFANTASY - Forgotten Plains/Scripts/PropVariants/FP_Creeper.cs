using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Minifantasy.ForgottenPlains
{
    public class FP_Creeper : MonoBehaviour
    {
        [Tooltip("Select a Prop Variant.")]
        [SerializeField] private CreeperSelection selection = CreeperSelection.Creeper1;

        [Header("Sprites")]
        [SerializeField] private Sprite creeper1;
        [SerializeField] private Sprite creeper2;
        [SerializeField] private Sprite creeper3;
        [SerializeField] private Sprite creeper4;
        [SerializeField] private Sprite creeper5;
        [SerializeField] private Sprite creeper6;
        [SerializeField] private Sprite creeper7;
        [SerializeField] private Sprite creeper8;

        [Header("Shadows")]
        [SerializeField] private Sprite shadowCreeper1;
        [SerializeField] private Sprite shadowCreeper2;
        [SerializeField] private Sprite shadowCreeper3;
        [SerializeField] private Sprite shadowCreeper4;
        [SerializeField] private Sprite shadowCreeper5;
        [SerializeField] private Sprite shadowCreeper6;
        [SerializeField] private Sprite shadowCreeper7;
        [SerializeField] private Sprite shadowCreeper8;

        private void OnValidate()
        {
            Sprite selectedSprite = null;
            Sprite selectedShadow = null;

            switch (selection)
            {
                case CreeperSelection.Creeper1:
                    selectedSprite = creeper1;
                    selectedShadow = shadowCreeper1;
                    break;
                case CreeperSelection.Creeper2:
                    selectedSprite = creeper2;
                    selectedShadow = shadowCreeper2;
                    break;
                case CreeperSelection.Creeper3:
                    selectedSprite = creeper3;
                    selectedShadow = shadowCreeper3;
                    break;
                case CreeperSelection.Creeper4:
                    selectedSprite = creeper4;
                    selectedShadow = shadowCreeper4;
                    break;
                case CreeperSelection.Creeper5:
                    selectedSprite = creeper5;
                    selectedShadow = shadowCreeper5;
                    break;
                case CreeperSelection.Creeper6:
                    selectedSprite = creeper6;
                    selectedShadow = shadowCreeper6;
                    break;
                case CreeperSelection.Creeper7:
                    selectedSprite = creeper7;
                    selectedShadow = shadowCreeper7;
                    break;
                case CreeperSelection.Creeper8:
                    selectedSprite = creeper8;
                    selectedShadow = shadowCreeper8;
                    break;
            }
            GetComponent<SpriteRenderer>().sprite = selectedSprite;
            transform.Find("Shadow").GetComponent<SpriteRenderer>().sprite = selectedShadow;
        }

        private enum CreeperSelection
        {
            Creeper1,
            Creeper2,
            Creeper3,
            Creeper4,
            Creeper5,
            Creeper6,
            Creeper7,
            Creeper8,
        }
    }
}