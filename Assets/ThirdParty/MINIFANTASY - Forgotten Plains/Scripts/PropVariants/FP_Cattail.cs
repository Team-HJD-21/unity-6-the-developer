using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Minifantasy.ForgottenPlains
{
    public class FP_Cattail : MonoBehaviour
    {
        [Tooltip("Select a Prop Variant.")]
        [SerializeField] private CattailSelection selection = CattailSelection.Cattail1;

        [Header("Sprites")]
        [SerializeField] private Sprite cattail1;
        [SerializeField] private Sprite cattail2;

        [Header("Shadows")]
        [SerializeField] private Sprite shadowCattail1;
        [SerializeField] private Sprite shadowCattail2;

        private void OnValidate()
        {
            Sprite selectedSprite = null;
            Sprite selectedShadow = null;

            switch (selection)
            {
                case CattailSelection.Cattail1:
                    selectedSprite = cattail1;
                    selectedShadow = shadowCattail1;
                    break;
                case CattailSelection.Cattail2:
                    selectedSprite = cattail2;
                    selectedShadow = shadowCattail2;
                    break;
            }
            GetComponent<SpriteRenderer>().sprite = selectedSprite;
            transform.Find("Shadow").GetComponent<SpriteRenderer>().sprite = selectedShadow;
        }

        private enum CattailSelection
        {
            Cattail1,
            Cattail2,
        }
    }
}