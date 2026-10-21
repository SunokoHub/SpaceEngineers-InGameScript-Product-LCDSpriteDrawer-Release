using System;
using System.Collections.Generic;

namespace IngameScript
{
    public sealed class DrawTarget
    {
        public long BlockId;
        public int Index;
        public List<SpriteDrawingElement> Elements;
        public List<LayoutElement> Layout;
        public BindingSet Binding;
        public bool RefreshPending = true;
    }

    public static class DrawingData
    {
        public static List<SpriteDrawingElement> Build(List<LayoutElement> elements)
        {
            var result = new List<SpriteDrawingElement>();
            foreach (var element in elements)
                result.Add(new SpriteDrawingElement {
                    Kind = element.IsText ? SpriteDrawingKind.Text : SpriteDrawingKind.Texture,
                    Data = element.Data, Position = element.Position,
                    Size = element.IsText ? (VRageMath.Vector2?)null : element.Size,
                    FontId = element.IsText ? element.FontId : null, Color = element.Color,
                    Alignment = element.Alignment == "LEFT" ? VRage.Game.GUI.TextPanel.TextAlignment.LEFT :
                        element.Alignment == "RIGHT" ? VRage.Game.GUI.TextPanel.TextAlignment.RIGHT :
                        VRage.Game.GUI.TextPanel.TextAlignment.CENTER,
                    RotationOrScale = element.IsText ? element.TextScale :
                        (float)((double)element.AngleDegrees * Math.PI / 180)
                });
            return result;
        }
    }
}
