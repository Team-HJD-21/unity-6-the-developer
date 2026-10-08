using System.Collections;
using UnityEngine;

namespace Minifantasy.Dungeon
{
    public class DUN_StaggerAnimation : MonoBehaviour
    {
        [SerializeField] AnimationClip animToStagger;
        private Animator myAnimator;
        private float staggerTime = 0f;

        void Start()
        {
            myAnimator = GetComponent<Animator>();
            myAnimator.enabled = false;

            if (animToStagger != null)
                staggerTime = animToStagger.length / 2;
            StartCoroutine(TurnOn());
        }
        IEnumerator TurnOn()
        {
            yield return new WaitForSeconds(staggerTime);
            myAnimator.enabled = true;
        }
    }
}
