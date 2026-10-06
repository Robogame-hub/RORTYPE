using UnityEngine;
using UnityEngine.UI;

namespace RorType.Gameplay.UI
{
    public sealed class PlayerStatusHudView : MonoBehaviour
    {
        public Canvas canvas;
        public Text ammoLabel, moneyLabel, healthLabel, shieldLabel;
        public Image healthFill, shieldFill, staminaFill;
        public GameObject shieldRoot;
        public Image[] skillSlots;
        public Text[] skillCooldownLabels, skillKeyLabels, skillStatusLabels;
        public Image[] dashCharges;
        public Graphic[] skillIcons;
        [Header("Reticle")]
        public RectTransform reticle;
        public Graphic reticleGraphic;
        public Graphic reticleDot;
        public RectTransform actualAimMarker;
        [Min(0f)] public float reticleSpreadScalePerDegree = 0.15f;
        [Min(0.01f)] public float pointSelectionReticleScale = 1.3f;

        [Header("Reticle Colors")]
        public Color reticleIdleColor = new Color(0.89f, 0.86f, 0.77f);
        public Color reticlePreparingColor = new Color(0.74f, 0.59f, 0.35f);
        public Color reticleReadyColor = new Color(0.89f, 0.86f, 0.77f);
        public Color reticleInvalidColor = new Color(0.94f, 0.28f, 0.2f);
        public Text weaponStatusLabel;

        public void SetReticleColor(Color tint)
        {
            if (reticleGraphic != null) reticleGraphic.color = tint;
            if (reticleDot != null) reticleDot.color = tint;
        }
    }
}
