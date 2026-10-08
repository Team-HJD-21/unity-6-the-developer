using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Minifantasy.ForgottenPlains
{
    public class FP_Flower : MonoBehaviour
    {
        [Tooltip("Select a Prop Variant.")]
        [SerializeField] private FlowerSelection selection = FlowerSelection.FlowerRed;

        [Header("Sprites")]
        [SerializeField] private Sprite flowerRed;
        [SerializeField] private Sprite flowerWhite;
        [SerializeField] private Sprite flowerPink;

        [Header("Shadows")]
        [SerializeField] private Sprite shadowFlowerRed;
        [SerializeField] private Sprite shadowFlowerWhite;
        [SerializeField] private Sprite shadowFlowerPink;

        private void OnValidate()
        {
            Sprite selectedSprite = null;
            Sprite selectedShadow = null;

            switch (selection)
            {
                case FlowerSelection.FlowerRed:
                    selectedSprite = flowerRed;
                    selectedShadow = shadowFlowerRed;
                    break;
                case FlowerSelection.FlowerWhite:
                    selectedSprite = flowerWhite;
                    selectedShadow = shadowFlowerWhite;
                    break;
                case FlowerSelection.FlowerPink:
                    selectedSprite = flowerPink;
                    selectedShadow = shadowFlowerPink;
                    break;
            }
            GetComponent<SpriteRenderer>().sprite = selectedSprite;
            transform.Find("Shadow").GetComponent<SpriteRenderer>().sprite = selectedShadow;
        }

        private enum FlowerSelection
        {
            FlowerRed,
            FlowerWhite,
            FlowerPink,
        }
    }
}