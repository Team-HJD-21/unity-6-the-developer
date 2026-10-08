using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Minifantasy
{
    public class SetAnimatorParameter : MonoBehaviour
    {
        private Animator animator;

        public string parameterName = "Idle";
        public float x = 1;
        public float y = 1;
        public float waitTime = 0f;

        private void Start()
        {
            animator = GetComponentInChildren<Animator>();
            Invoke("ToggleAnimatorParameter", waitTime);
            ToggleDirection();
        }

        public void ToggleAnimatorParameter()
        {
            animator.SetBool(parameterName, true);
        }

        public void ToggleDirection()
        {
            animator.SetFloat("X", x);
            animator.SetFloat("Y", y);
        }
    }
}