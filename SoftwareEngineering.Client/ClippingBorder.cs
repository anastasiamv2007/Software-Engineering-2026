using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SoftwareEngineering.Client;

/// <summary>
/// Border, який обрізає вміст по своїх заокруглених кутах.
/// Звичайний Border лише малює заокруглену рамку, а вміст (таблиця, її смуга прокрутки) лишається прямокутним
/// і вилазить за кути — особливо помітно на темних темах.
/// </summary>
public class ClippingBorder : Border
{
    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);

        if (this.Child is { } child)
        {
            // Вміст лежить усередині рамки, тому радіус обрізання менший на її товщину
            var radius = Math.Max(0, this.CornerRadius.TopLeft - this.BorderThickness.Left);
            child.Clip = new RectangleGeometry(new Rect(child.RenderSize), radius, radius);
        }

        return size;
    }
}
