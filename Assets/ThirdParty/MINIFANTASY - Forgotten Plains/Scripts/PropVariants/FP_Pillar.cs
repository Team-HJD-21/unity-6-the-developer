using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Minifantasy.ForgottenPlains
{
    public class FP_Pillar : MonoBehaviour
    {
        [Tooltip("Select a Prop Variant.")]
        [SerializeField] private PillarSelection selection = PillarSelection.Tall1;

        [Header("Sprites")]
        [SerializeField] private Sprite tall1;
        [SerializeField] private Sprite tall2;
        [SerializeField] private Sprite tall3;
        [SerializeField] private Sprite medium1;
        [SerializeField] private Sprite medium2;
        [SerializeField] private Sprite medium3;
        [SerializeField] private Sprite medium4;
        [SerializeField] private Sprite short1;
        [SerializeField] private Sprite short2;

        [Header("Shadows")]
        [SerializeField] private Sprite shadowTall1;
        [SerializeField] private Sprite shadowTall2;
        [SerializeField] private Sprite shadowTall3;
        [SerializeField] private Sprite shadowMedium1;
        [SerializeField] private Sprite shadowMedium2;
        [SerializeField] private Sprite shadowMedium3;
        [SerializeField] private Sprite shadowMedium4;
        [SerializeField] private Sprite shadowShort1;
        [SerializeField] private Sprite shadowShort2;

        private void OnValidate()
        {
            Sprite selectedSprite = null;
            Sprite selectedShadow = null;

            switch (selection)
            {
                case PillarSelection.Tall1:
                    selectedSprite = tall1;
                    selectedShadow = shadowTall1;
                    break;
                case PillarSelection.Tall2:
                    selectedSprite = tall2;
                    selectedShadow = shadowTall2;
                    break;
                case PillarSelection.Tall3:
                    selectedSprite = tall3;
                    selectedShadow = shadowTall3;
                    break;
                case PillarSelection.Medium1:
                    selectedSprite = medium1;
                    selectedShadow = shadowMedium1;
                    break;
                case PillarSelection.Medium2:
                    selectedSprite = medium2;
                    selectedShadow = shadowMedium2;
                    break;
                case PillarSelection.Medium3:
                    selectedSprite = medium3;
                    selectedShadow = shadowMedium3;
                    break;
                case PillarSelection.Medium4:
                    selectedSprite = medium4;
                    selectedShadow = shadowMedium4;
                    break;
                case PillarSelection.Short1:
                    selectedSprite = short1;
                    selectedShadow = shadowShort1;
                    break;
                case PillarSelection.Short2:
                    selectedSprite = short2;
                    selectedShadow = shadowShort2;
                    break;

            }
            GetComponent<SpriteRenderer>().sprite = selectedSprite;
            transform.Find("Shadow").GetComponent<SpriteRenderer>().sprite = selectedShadow;
        }

        private enum PillarSelection
        {
            Tall1,
            Tall2,
            Tall3,
            Medium1,
            Medium2,
            Medium3,
            Medium4,
            Short1,
            Short2,
        }
    }
}