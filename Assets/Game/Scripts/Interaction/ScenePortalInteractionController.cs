using RorType.Gameplay.Player;
using RorType.Gameplay.UI;
using UnityEngine;

namespace RorType.Gameplay.Interaction
{
    [RequireComponent(typeof(TopDownInputAdapter))]
    public sealed class ScenePortalInteractionController : MonoBehaviour
    {
        private TopDownInputAdapter inputAdapter;

        private void Awake()
        {
            inputAdapter = GetComponent<TopDownInputAdapter>();
        }

        private void Update()
        {
            if (PortalUiRuntime.IsChoiceOpen)
            {
                UpdatePortalHighlights();
                PortalUiRuntime.HidePrompt();
                inputAdapter.ConsumeInteractPressed();
                return;
            }

            var activePortal = ResolveClosestPortal();
            UpdatePortalHighlights();
            if (activePortal != null)
            {
                PortalUiRuntime.ShowPrompt(activePortal.GetInteractionPrompt());
                if (!inputAdapter.InteractPressed)
                {
                    return;
                }

                inputAdapter.ConsumeInteractPressed();
                activePortal.Interact();
                return;
            }

            var activeInteractable = ResolveClosestInteractable();
            if (activeInteractable == null)
            {
                PortalUiRuntime.HidePrompt();
                inputAdapter.ConsumeInteractPressed();
                return;
            }

            PortalUiRuntime.ShowPrompt(activeInteractable.GetInteractionPrompt());
            if (!inputAdapter.InteractPressed)
            {
                return;
            }

            inputAdapter.ConsumeInteractPressed();
            activeInteractable.Interact(this);
        }

        private void UpdatePortalHighlights()
        {
            var portals = ScenePortal.ActivePortals;
            for (var index = 0; index < portals.Count; index++)
            {
                var portal = portals[index];
                if (portal == null)
                {
                    continue;
                }

                var shouldHighlight =
                    portal.isActiveAndEnabled &&
                    portal.IsTouchedBy(this);

                portal.SetHighlighted(shouldHighlight);
            }
        }

        private ScenePortal ResolveClosestPortal()
        {
            var portals = ScenePortal.ActivePortals;
            ScenePortal closestPortal = null;
            var closestDistance = float.MaxValue;

            for (var index = 0; index < portals.Count; index++)
            {
                var portal = portals[index];
                if (portal == null || !portal.isActiveAndEnabled)
                {
                    continue;
                }

                if (!portal.IsTouchedBy(this))
                {
                    continue;
                }

                var sqrDistance = portal.GetSqrDistanceTo(transform.position);
                if (sqrDistance >= closestDistance)
                {
                    continue;
                }

                closestDistance = sqrDistance;
                closestPortal = portal;
            }

            return closestPortal;
        }

        private WorldInteractable ResolveClosestInteractable()
        {
            var interactables = WorldInteractable.ActiveInteractables;
            WorldInteractable closestInteractable = null;
            var closestDistance = float.MaxValue;

            for (var index = 0; index < interactables.Count; index++)
            {
                var interactable = interactables[index];
                if (interactable == null || !interactable.IsAvailable)
                {
                    continue;
                }

                if (!interactable.IsTouchedBy(this))
                {
                    continue;
                }

                var sqrDistance = interactable.GetSqrDistanceTo(transform.position);
                if (sqrDistance >= closestDistance)
                {
                    continue;
                }

                closestDistance = sqrDistance;
                closestInteractable = interactable;
            }

            return closestInteractable;
        }
    }
}
