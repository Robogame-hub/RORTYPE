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
        public RectTransform reticle;
    }
}
