using UnityEngine;

namespace RorType.Gameplay.Combat
{
    public static class RuntimeRendererUtility
    {
        private static MaterialPropertyBlock sharedPropertyBlock;

        public static void SetColor(Renderer renderer, Color color)
        {
            if (renderer == null)
            {
                return;
            }

            sharedPropertyBlock ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(sharedPropertyBlock);
            sharedPropertyBlock.SetColor("_BaseColor", color);
            sharedPropertyBlock.SetColor("_Color", color);
            renderer.SetPropertyBlock(sharedPropertyBlock);
        }
    }
}
