using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Minifantasy.ForgottenPlains
{
    public class FP_GrassLong : MonoBehaviour
    {
        [Tooltip("Select a Prop Variant.")]
        [SerializeField] private GrassSelection selection = GrassSelection.GrassLong1;

        [Header("Sprites")]
        [SerializeField] private Sprite grassLong1;
        [SerializeField] private Sprite grassLong2;

        [Header("Shadows")]
        [SerializeField] private Sprite shadowGrassLong1;
        [SerializeField] private Sprite shadowGrassLong2;

        private void OnValidate()
        {
            Sprite selectedSprite = null;
            Sprite selectedShadow = null;

            switch (selection)
            {
                case GrassSelection.GrassLong1:
                    selectedSprite = grassLong1;
                    selectedShadow = shadowGrassLong1;
                    break;
                case GrassSelection.GrassLong2:
                    selectedSprite = grassLong2;
                    selectedShadow = shadowGrassLong2;
                    break;
            }
            GetComponent<SpriteRenderer>().sprite = selectedSprite;
            transform.Find("Shadow").GetComponent<SpriteRenderer>().sprite = selectedShadow;
        }

        private enum GrassSelection
        {
            GrassLong1,
            GrassLong2,
        }
    }
}