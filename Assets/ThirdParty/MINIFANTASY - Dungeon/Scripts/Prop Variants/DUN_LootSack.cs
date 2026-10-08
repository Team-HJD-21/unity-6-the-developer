using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Minifantasy.Dungeon
{
    public class DUN_LootSack : MonoBehaviour
    {
        [SerializeField] private bool knocked_Over = false;

        [Header("Sprites")]
        [SerializeField] private Sprite loot_sack;
        [SerializeField] private Sprite loot_sack_knocked_over;

        [Header("Shadows")]
        [SerializeField] private Sprite loot_sack_upright_shadow;
        [SerializeField] private Sprite loot_sack_knocked_over_shadow;

        private void OnValidate()
        {
            Sprite selectedSprite = null;
            Sprite selectedShadow = null;

            switch (knocked_Over)
            {
                case false:
                    selectedSprite = loot_sack;
                    selectedShadow = loot_sack_upright_shadow;
                    break;
                case true:
                    selectedSprite = loot_sack_knocked_over;
                    selectedShadow = loot_sack_knocked_over_shadow;
                    break;
            }

            GetComponent<SpriteRenderer>().sprite = selectedSprite;
            transform.Find("Shadow").GetComponent<SpriteRenderer>().sprite = selectedShadow;
        }
    }
}