using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Minifantasy.Dungeon
{
    public class DUN_Chair : MonoBehaviour
    {
        [Tooltip("Select a Chair.")]
        [SerializeField] private bool knocked_Over = false;
        [SerializeField] private Direction direction = Direction.West;
        [SerializeField] private KnockedOverDirection knockedoverdirection = KnockedOverDirection.West;

        [Header("Sprites")]
        [SerializeField] private Sprite chair_west;
        [SerializeField] private Sprite chair_west_knocked_over;
        [SerializeField] private Sprite chair_east;
        [SerializeField] private Sprite chair_east_knocked_over;
        [SerializeField] private Sprite chair_north;
        [SerializeField] private Sprite chair_south;

        [Header("Shadows")]
        [SerializeField] private Sprite chair_west_shadow;
        [SerializeField] private Sprite chair_west_knocked_over_shadow;
        [SerializeField] private Sprite chair_east_shadow;
        [SerializeField] private Sprite chair_east_knocked_over_shadow;
        [SerializeField] private Sprite chair_north_shadow;
        [SerializeField] private Sprite chair_south_shadow;

        private void OnValidate()
        {
            Sprite selectedSprite = null;
            Sprite selectedShadow = null;

            if (!knocked_Over)
            {
                switch (direction)
                {
                    case Direction.West:
                        selectedSprite = chair_west;
                        selectedShadow = chair_west_shadow;
                        break;
                    case Direction.North:
                        selectedSprite = chair_north;
                        selectedShadow = chair_north_shadow;
                        break;
                    case Direction.East:
                        selectedSprite = chair_east;
                        selectedShadow = chair_east_shadow;
                        break;
                    case Direction.South:
                        selectedSprite = chair_south;
                        selectedShadow = chair_south_shadow;
                        break;
                }
            }
            else
            {
                switch (knockedoverdirection)
                {
                    case KnockedOverDirection.West:
                        selectedSprite = chair_west_knocked_over;
                        selectedShadow = chair_west_knocked_over_shadow;
                        break;
                    case KnockedOverDirection.East:
                        selectedSprite = chair_east_knocked_over;
                        selectedShadow = chair_east_knocked_over_shadow;
                        break;
                }
            }

            GetComponent<SpriteRenderer>().sprite = selectedSprite;
            transform.Find("Shadow").GetComponent<SpriteRenderer>().sprite = selectedShadow;
        }

        private enum Direction
        {
            West,
            North,
            East,
            South,
        }

        private enum KnockedOverDirection
        {
            West,
            East,
        }
    }
}