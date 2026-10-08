using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Minifantasy.Dungeon
{
    public class DUN_Table : MonoBehaviour
    {
        [Tooltip("Select a Table.")]
        [SerializeField] private bool broken = false;
        [SerializeField] private Size direction = Size.Small;
        [SerializeField] private BrokenDirection brokenDirection = BrokenDirection.West;

        [Header("Sprites")]
        [SerializeField] private Sprite small_table;
        [SerializeField] private Sprite small_table_broken_west;
        [SerializeField] private Sprite small_table_broken_east;
        [SerializeField] private Sprite wide_table;
        [SerializeField] private Sprite wide_table_broken;
        [SerializeField] private Sprite long_table;
        [SerializeField] private Sprite long_table_broken_west;
        [SerializeField] private Sprite long_table_broken_east;
        [SerializeField] private Sprite large_table;
        [SerializeField] private Sprite large_table_broken;

        [Header("Shadows")]
        [SerializeField] private Sprite small_table_shadow;
        [SerializeField] private Sprite small_table_broken_west_shadow;
        [SerializeField] private Sprite small_table_broken_east_shadow;
        [SerializeField] private Sprite wide_table_shadow;
        [SerializeField] private Sprite wide_table_broken_shadow;
        [SerializeField] private Sprite long_table_shadow;
        [SerializeField] private Sprite long_table_broken_west_shadow;
        [SerializeField] private Sprite long_table_broken_east_shadow;
        [SerializeField] private Sprite large_table_shadow;
        [SerializeField] private Sprite large_table_broken_shadow;

        private void OnValidate()
        {
            Sprite selectedSprite = null;
            Sprite selectedShadow = null;

            if (!broken)
            {
                switch (direction)
                {
                    case Size.Small:
                        selectedSprite = small_table;
                        selectedShadow = small_table_shadow;
                        break;
                    case Size.Wide:
                        selectedSprite = wide_table;
                        selectedShadow = wide_table_shadow;
                        break;
                    case Size.Long:
                        selectedSprite = long_table;
                        selectedShadow = long_table_shadow;
                        break;
                    case Size.Large:
                        selectedSprite = large_table;
                        selectedShadow = large_table_shadow;
                        break;
                }
            }
            else
            {
                switch (direction)
                {
                    case Size.Small:
                        switch (brokenDirection)
                        {
                            case BrokenDirection.West:
                                selectedSprite = small_table_broken_west;
                                selectedShadow = small_table_broken_west_shadow;
                                break;
                            case BrokenDirection.East:
                                selectedSprite = small_table_broken_east;
                                selectedShadow = small_table_broken_east_shadow;
                                break;
                        }
                        break;
                    case Size.Long:
                        switch (brokenDirection)
                        {
                            case BrokenDirection.West:
                                selectedSprite = long_table_broken_west;
                                selectedShadow = long_table_broken_west_shadow;
                                break;
                            case BrokenDirection.East:
                                selectedSprite = long_table_broken_east;
                                selectedShadow = long_table_broken_east_shadow;
                                break;
                        }
                        break;
                    case Size.Wide:
                        selectedSprite = wide_table_broken;
                        selectedShadow = wide_table_broken_shadow;
                        break;
                    case Size.Large:
                        selectedSprite = large_table_broken;
                        selectedShadow = large_table_broken_shadow;
                        break;
                }
            }

            GetComponent<SpriteRenderer>().sprite = selectedSprite;
            transform.Find("Shadow").GetComponent<SpriteRenderer>().sprite = selectedShadow;
        }

        private enum Size
        {
            Small,
            Wide,
            Long,
            Large,
        }

        private enum BrokenDirection
        {
            West,
            East,
        }
    }
}