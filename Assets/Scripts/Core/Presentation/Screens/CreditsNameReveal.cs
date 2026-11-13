using TMPro;
using UnityEngine;

namespace TeamHJD.Game.Presentation
{
    public sealed class CreditsNameReveal : MonoBehaviour
    {
        public TMP_Text Text;
        public RectTransform RevealClip;
        public float RevealWidth = 400;
        public void Fade(float alpha)
        {
            var color = Text.color; color.a = Mathf.Clamp01(alpha); Text.color = color;
        }
        public void Reveal(float progress)
        {
            if (RevealClip != null)
                RevealClip.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, RevealWidth * Mathf.Clamp01(progress));
        }
    }
}
