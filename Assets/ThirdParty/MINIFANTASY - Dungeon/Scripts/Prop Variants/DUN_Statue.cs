using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Minifantasy.Dungeon
{
    public class DUN_Statue : MonoBehaviour
    {
        [Tooltip("Select a Statue.")]
        [SerializeField] private Direction direction = Direction.West;
        [SerializeField] private ArmorType armorType = ArmorType.Iron;

        [Header("Sprites")]
        [SerializeField] private Sprite iron_east;
        [SerializeField] private Sprite iron_north;
        [SerializeField] private Sprite iron_south;
        [SerializeField] private Sprite iron_west;
        [SerializeField] private Sprite steel_east;
        [SerializeField] private Sprite steel_north;
        [SerializeField] private Sprite steel_south;
        [SerializeField] private Sprite steel_west;
        [SerializeField] private Sprite shiny_east;
        [SerializeField] private Sprite shiny_north;
        [SerializeField] private Sprite shiny_south;
        [SerializeField] private Sprite shiny_west;

        [SerializeField] private Sprite soldier_statue_shadow;

        private void OnValidate()
        {
            Sprite selectedSprite = null;

            switch (direction)
            {
                case Direction.West:
                    switch (armorType)
                    {
                        case ArmorType.Iron:
                            selectedSprite = iron_west;
                            break;
                        case ArmorType.Steel:
                            selectedSprite = steel_west;
                            break;
                        case ArmorType.Shiny:
                            selectedSprite = shiny_west;
                            break;
                    }
                    break;
                case Direction.North:
                    switch (armorType)
                    {
                        case ArmorType.Iron:
                            selectedSprite = iron_north;
                            break;
                        case ArmorType.Steel:
                            selectedSprite = steel_north;
                            break;
                        case ArmorType.Shiny:
                            selectedSprite = shiny_north;
                            break;
                    }
                    break;
                case Direction.East:
                    switch (armorType)
                    {
                        case ArmorType.Iron:
                            selectedSprite = iron_east;
                            break;
                        case ArmorType.Steel:
                            selectedSprite = steel_east;
                            break;
                        case ArmorType.Shiny:
                            selectedSprite = shiny_east;
                            break;
                    }
                    break;
                case Direction.South:
                    switch (armorType)
                    {
                        case ArmorType.Iron:
                            selectedSprite = iron_south;
                            break;
                        case ArmorType.Steel:
                            selectedSprite = steel_south;
                            break;
                        case ArmorType.Shiny:
                            selectedSprite = shiny_south;
                            break;
                    }
                    break;
            }

            GetComponent<SpriteRenderer>().sprite = selectedSprite;
            transform.Find("Shadow").GetComponent<SpriteRenderer>().sprite = soldier_statue_shadow;
        }

        private enum Direction
        {
            West,
            North,
            East,
            South,
        }

        private enum ArmorType
        {
            Iron,
            Steel,
            Shiny,
        }
    }
}